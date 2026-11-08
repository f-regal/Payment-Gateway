using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentsRepository paymentsRepository,
        PaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _paymentsRepository = paymentsRepository;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessPayment(PostPaymentRequest request)
    {
        PaymentResult result;

        try
        {
            result = await _paymentService.ProcessPaymentAsync(request);
        }
        catch (BankUnavailableException ex)
        {
            _logger.LogWarning(ex, "Payment could not be processed because the acquiring bank is unavailable.");

            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The acquiring bank is unavailable.",
                detail: "The payment was not processed. Please try again later.");
        }

        if (result.IsRejected)
        {
            return BadRequest(new RejectedPaymentResponse(result.Errors));
        }

        return Ok(PaymentResponse.From(result.Payment!));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(PaymentResponse.From(payment));
    }
}
