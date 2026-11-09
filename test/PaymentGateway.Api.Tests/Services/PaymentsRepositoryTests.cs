using PaymentGateway.Api.Models;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Services;

public class PaymentsRepositoryTests
{
    private readonly PaymentsRepository _repository = new();

    [Fact]
    public void Get_ReturnsPaymentThatWasAdded()
    {
        var payment = CreatePayment();

        _repository.Add(payment);

        Assert.Equal(payment, _repository.Get(payment.Id));
    }

    [Fact]
    public void Get_ReturnsNull_WhenPaymentDoesNotExist()
    {
        Assert.Null(_repository.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Add_Throws_WhenIdAlreadyExists()
    {
        var payment = CreatePayment();
        _repository.Add(payment);

        Assert.Throws<InvalidOperationException>(() => _repository.Add(payment with { Amount = 999 }));
        Assert.Equal(payment, _repository.Get(payment.Id));
    }

    [Fact]
    public void Add_IsSafeUnderConcurrentWrites()
    {
        var payments = Enumerable.Range(0, 1000).Select(_ => CreatePayment()).ToList();

        Parallel.ForEach(payments, payment => _repository.Add(payment));

        Assert.All(payments, payment => Assert.Equal(payment, _repository.Get(payment.Id)));
    }

    private static Payment CreatePayment() => new(
        Id: Guid.NewGuid(),
        Status: PaymentStatus.Authorized,
        CardNumberLastFour: "8877",
        ExpiryMonth: 4,
        ExpiryYear: 2027,
        Currency: "GBP",
        Amount: 100);
}
