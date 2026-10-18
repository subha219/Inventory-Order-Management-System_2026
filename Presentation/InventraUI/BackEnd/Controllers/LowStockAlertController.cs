using InventraUI.BackEnd.Common.Models;
using InventraUI.BackEnd.Common.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventraUI.BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LowStockAlertController : ControllerBase
{
    private readonly LowStockAlertSender _alertSender;

    public LowStockAlertController(LowStockAlertSender alertSender)
    {
        _alertSender = alertSender;
    }

    [Authorize(Roles = "Products")]
    [HttpPost("SendNow")]
    public async Task<ActionResult<ApiSuccessResult<LowStockAlertResult>>> SendNowAsync(CancellationToken cancellationToken)
    {
        var response = await _alertSender.SendAsync(cancellationToken);

        return Ok(new ApiSuccessResult<LowStockAlertResult>
        {
            Code = StatusCodes.Status200OK,
            Message = $"Success executing {nameof(SendNowAsync)}",
            Content = response
        });
    }
}
