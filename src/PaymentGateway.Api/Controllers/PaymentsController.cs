using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Exceptions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class PaymentsController : Controller
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly PaymentService _paymentService;

    public PaymentsController(IPaymentsRepository paymentsRepository, PaymentService paymentService)
    {
        _paymentsRepository = paymentsRepository;
        _paymentService = paymentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RejectedPaymentResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ProcessPayment(PostPaymentRequest request)
    {
        PaymentResult result;

        try
        {
            result = await _paymentService.ProcessPaymentAsync(request);
        }
        catch (BankUnavailableException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The acquiring bank is unavailable.",
                detail: "The payment was not processed. Please try again later.");
        }

        if (result.IsRejected)
        {
            return BadRequest(new RejectedPaymentResponse(result.Errors));
        }

        var response = PaymentResponse.From(result.Payment!);

        return CreatedAtAction(nameof(GetPayment), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
