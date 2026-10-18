using Application.Common.Extensions;
using Application.Common.Repositories;
using Application.Features.NumberSequenceManager;
using Domain.Entities;
using MediatR;

namespace Application.Features.ImportManager.Commands;

/// <summary>One CSV row for a vendor or a customer (both have the same columns).</summary>
public class PartyImportRow
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? ZipCode { get; init; }
    public string? Country { get; init; }
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public string? Website { get; init; }
    public string? Group { get; init; }
    public string? Category { get; init; }
}

public class ImportVendorsRequest : IRequest<ImportResult>
{
    public List<PartyImportRow>? Rows { get; init; }
    public bool DryRun { get; init; }
    public string? CreatedById { get; init; }
}

public class ImportCustomersRequest : IRequest<ImportResult>
{
    public List<PartyImportRow>? Rows { get; init; }
    public bool DryRun { get; init; }
    public string? CreatedById { get; init; }
}

internal static class PartyImportChecks
{
    /// <summary>Returns an error text, or null when the row is usable.</summary>
    public static string? Check(PartyImportRow r)
    {
        var name = (r.Name ?? "").Trim();
        if (name.Length == 0) return "Name is empty.";
        if (name.Length > 255) return "Name is longer than 255 characters.";
        var email = (r.EmailAddress ?? "").Trim();
        if (email.Length > 0 && (!email.Contains('@') || email.StartsWith("@") || email.EndsWith("@") || email.Contains(' '))) return "E-mail address does not look valid.";
        foreach (var (label, v) in new[] { ("Street", r.Street), ("City", r.City), ("State", r.State), ("Zip code", r.ZipCode), ("Country", r.Country), ("Phone", r.PhoneNumber), ("Website", r.Website) })
            if ((v ?? "").Length > 255) return $"{label} is longer than 255 characters.";
        return null;
    }

    public static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public class ImportVendorsHandler : IRequestHandler<ImportVendorsRequest, ImportResult>
{
    private readonly ICommandRepository<Vendor> _vendors;
    private readonly ICommandRepository<VendorGroup> _groups;
    private readonly ICommandRepository<VendorCategory> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public ImportVendorsHandler(ICommandRepository<Vendor> vendors, ICommandRepository<VendorGroup> groups,
        ICommandRepository<VendorCategory> categories, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _vendors = vendors; _groups = groups; _categories = categories; _unitOfWork = unitOfWork; _numbers = numbers;
    }

    public async Task<ImportResult> Handle(ImportVendorsRequest request, CancellationToken ct)
    {
        var rows = request.Rows ?? new List<PartyImportRow>();
        if (rows.Count == 0) throw new Exception("The file has no rows.");
        if (rows.Count > 3000) throw new Exception("Import at most 3000 rows at a time.");

        var result = new ImportResult { DryRun = request.DryRun };
        var existing = _vendors.GetQuery().ApplyIsDeletedFilter().Select(v => new { v.Name, v.EmailAddress }).ToList();
        var names = new HashSet<string>(existing.Where(x => x.Name != null).Select(x => x.Name!), StringComparer.OrdinalIgnoreCase);
        var emails = new HashSet<string>(existing.Where(x => !string.IsNullOrEmpty(x.EmailAddress)).Select(x => x.EmailAddress!), StringComparer.OrdinalIgnoreCase);
        var groups = _groups.GetQuery().ApplyIsDeletedFilter().ToList();
        var categories = _categories.GetQuery().ApplyIsDeletedFilter().ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var res = new ImportRowResult { Row = i + 2 };
            result.Rows.Add(res);

            var problem = PartyImportChecks.Check(r);
            if (problem != null) { res.Status = "Error"; res.Message = problem; result.Errors++; continue; }
            var name = r.Name!.Trim();
            var email = (r.EmailAddress ?? "").Trim();
            if (names.Contains(name)) { res.Status = "Skipped"; res.Message = "A vendor with this name already exists."; result.Skipped++; continue; }
            if (email.Length > 0 && emails.Contains(email)) { res.Status = "Skipped"; res.Message = "A vendor with this e-mail address already exists."; result.Skipped++; continue; }
            names.Add(name);
            if (email.Length > 0) emails.Add(email);

            var groupName = string.IsNullOrWhiteSpace(r.Group) ? "General" : r.Group.Trim();
            var catName = string.IsNullOrWhiteSpace(r.Category) ? "General" : r.Category.Trim();
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
            var cat = categories.FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
            var notes = new List<string>();

            if (!request.DryRun)
            {
                if (group == null) { group = new VendorGroup { CreatedById = request.CreatedById, Name = groupName }; await _groups.CreateAsync(group, ct); groups.Add(group); }
                if (cat == null) { cat = new VendorCategory { CreatedById = request.CreatedById, Name = catName }; await _categories.CreateAsync(cat, ct); categories.Add(cat); }
                await _vendors.CreateAsync(new Vendor
                {
                    CreatedById = request.CreatedById, Number = _numbers.GenerateNumber(nameof(Vendor), "", "VND"), Name = name,
                    Description = PartyImportChecks.Clean(r.Description), Street = PartyImportChecks.Clean(r.Street), City = PartyImportChecks.Clean(r.City),
                    State = PartyImportChecks.Clean(r.State), ZipCode = PartyImportChecks.Clean(r.ZipCode), Country = PartyImportChecks.Clean(r.Country),
                    PhoneNumber = PartyImportChecks.Clean(r.PhoneNumber), EmailAddress = PartyImportChecks.Clean(r.EmailAddress), Website = PartyImportChecks.Clean(r.Website),
                    VendorGroupId = group.Id, VendorCategoryId = cat.Id
                }, ct);
            }
            else
            {
                if (group == null) notes.Add($"group \"{groupName}\" will be created");
                if (cat == null) notes.Add($"category \"{catName}\" will be created");
            }
            res.Message = notes.Count == 0 ? null : string.Join(", ", notes);
            result.Created++;
        }

        if (!request.DryRun && result.Created > 0) await _unitOfWork.SaveAsync(ct);
        return result;
    }
}

public class ImportCustomersHandler : IRequestHandler<ImportCustomersRequest, ImportResult>
{
    private readonly ICommandRepository<Customer> _customers;
    private readonly ICommandRepository<CustomerGroup> _groups;
    private readonly ICommandRepository<CustomerCategory> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NumberSequenceService _numbers;

    public ImportCustomersHandler(ICommandRepository<Customer> customers, ICommandRepository<CustomerGroup> groups,
        ICommandRepository<CustomerCategory> categories, IUnitOfWork unitOfWork, NumberSequenceService numbers)
    {
        _customers = customers; _groups = groups; _categories = categories; _unitOfWork = unitOfWork; _numbers = numbers;
    }

    public async Task<ImportResult> Handle(ImportCustomersRequest request, CancellationToken ct)
    {
        var rows = request.Rows ?? new List<PartyImportRow>();
        if (rows.Count == 0) throw new Exception("The file has no rows.");
        if (rows.Count > 3000) throw new Exception("Import at most 3000 rows at a time.");

        var result = new ImportResult { DryRun = request.DryRun };
        var existing = _customers.GetQuery().ApplyIsDeletedFilter().Select(v => new { v.Name, v.EmailAddress }).ToList();
        var names = new HashSet<string>(existing.Where(x => x.Name != null).Select(x => x.Name!), StringComparer.OrdinalIgnoreCase);
        var emails = new HashSet<string>(existing.Where(x => !string.IsNullOrEmpty(x.EmailAddress)).Select(x => x.EmailAddress!), StringComparer.OrdinalIgnoreCase);
        var groups = _groups.GetQuery().ApplyIsDeletedFilter().ToList();
        var categories = _categories.GetQuery().ApplyIsDeletedFilter().ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var res = new ImportRowResult { Row = i + 2 };
            result.Rows.Add(res);

            var problem = PartyImportChecks.Check(r);
            if (problem != null) { res.Status = "Error"; res.Message = problem; result.Errors++; continue; }
            var name = r.Name!.Trim();
            var email = (r.EmailAddress ?? "").Trim();
            if (names.Contains(name)) { res.Status = "Skipped"; res.Message = "A customer with this name already exists."; result.Skipped++; continue; }
            if (email.Length > 0 && emails.Contains(email)) { res.Status = "Skipped"; res.Message = "A customer with this e-mail address already exists."; result.Skipped++; continue; }
            names.Add(name);
            if (email.Length > 0) emails.Add(email);

            var groupName = string.IsNullOrWhiteSpace(r.Group) ? "General" : r.Group.Trim();
            var catName = string.IsNullOrWhiteSpace(r.Category) ? "General" : r.Category.Trim();
            var group = groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase));
            var cat = categories.FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
            var notes = new List<string>();

            if (!request.DryRun)
            {
                if (group == null) { group = new CustomerGroup { CreatedById = request.CreatedById, Name = groupName }; await _groups.CreateAsync(group, ct); groups.Add(group); }
                if (cat == null) { cat = new CustomerCategory { CreatedById = request.CreatedById, Name = catName }; await _categories.CreateAsync(cat, ct); categories.Add(cat); }
                await _customers.CreateAsync(new Customer
                {
                    CreatedById = request.CreatedById, Number = _numbers.GenerateNumber(nameof(Customer), "", "CST"), Name = name,
                    Description = PartyImportChecks.Clean(r.Description), Street = PartyImportChecks.Clean(r.Street), City = PartyImportChecks.Clean(r.City),
                    State = PartyImportChecks.Clean(r.State), ZipCode = PartyImportChecks.Clean(r.ZipCode), Country = PartyImportChecks.Clean(r.Country),
                    PhoneNumber = PartyImportChecks.Clean(r.PhoneNumber), EmailAddress = PartyImportChecks.Clean(r.EmailAddress), Website = PartyImportChecks.Clean(r.Website),
                    CustomerGroupId = group.Id, CustomerCategoryId = cat.Id
                }, ct);
            }
            else
            {
                if (group == null) notes.Add($"group \"{groupName}\" will be created");
                if (cat == null) notes.Add($"category \"{catName}\" will be created");
            }
            res.Message = notes.Count == 0 ? null : string.Join(", ", notes);
            result.Created++;
        }

        if (!request.DryRun && result.Created > 0) await _unitOfWork.SaveAsync(ct);
        return result;
    }
}
