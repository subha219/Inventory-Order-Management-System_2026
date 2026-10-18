using Application.Common.Services.EmailManager;
using InventraUI.BackEnd.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SmtpSettingsController : ControllerBase
{
    private readonly ISmtpSettingsStore _store;
    private readonly IEmailService _email;

    public SmtpSettingsController(ISmtpSettingsStore store, IEmailService email)
    {
        _store = store;
        _email = email;
    }

    public class TestMailRequest { public string? To { get; set; } }
    public class TestMailResult { public bool Sent { get; set; } public string Message { get; set; } = string.Empty; }

    [Authorize(Roles = "Users")]
    [HttpGet("Get")]
    public ActionResult<ApiSuccessResult<SmtpSettingsView>> Get()
        => Ok(new ApiSuccessResult<SmtpSettingsView> { Code = StatusCodes.Status200OK, Message = "Success executing Get", Content = _store.GetView() });

    [Authorize(Roles = "Users")]
    [HttpPost("Save")]
    public ActionResult<ApiSuccessResult<SmtpSettingsView>> Save(SmtpSettingsInput input)
    {
        _store.Save(input);
        return Ok(new ApiSuccessResult<SmtpSettingsView> { Code = StatusCodes.Status200OK, Message = "Success executing Save", Content = _store.GetView() });
    }

    /// <summary>Sends a short test message with the saved settings and reports what happened, so a wrong password or port is visible.</summary>
    [Authorize(Roles = "Users")]
    [HttpPost("SendTest")]
    public async Task<ActionResult<ApiSuccessResult<TestMailResult>>> SendTest(TestMailRequest r, CancellationToken ct)
    {
        TestMailResult result;
        if (string.IsNullOrWhiteSpace(r.To) || !r.To.Contains('@'))
            result = new TestMailResult { Sent = false, Message = "Enter the e-mail address to send the test to." };
        else
        {
            try
            {
                await _email.SendEmailWithAttachmentsAsync(r.To.Trim(), "Inventra test e-mail",
                    "<p>This is a test message from your warehouse system.</p><p>If you can read it, e-mail is set up correctly.</p>");
                result = new TestMailResult { Sent = true, Message = "Test e-mail sent to " + r.To.Trim() + ". Check the inbox (and spam)." };
            }
            catch (Exception ex)
            {
                result = new TestMailResult { Sent = false, Message = ex.Message };
            }
        }
        return Ok(new ApiSuccessResult<TestMailResult> { Code = StatusCodes.Status200OK, Message = "Success executing SendTest", Content = result });
    }
}
