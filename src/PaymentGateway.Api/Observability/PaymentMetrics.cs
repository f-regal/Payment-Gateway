using System.Diagnostics.Metrics;

namespace PaymentGateway.Api.Observability;

public class PaymentMetrics
{
    public const string MeterName = "PaymentGateway.Api";
    public const string PaymentsCounterName = "payments.processed";

    private readonly Counter<long> _payments;

    public PaymentMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _payments = meter.CreateCounter<long>(PaymentsCounterName, description: "Number of payment requests, by outcome.");
    }

    public void RecordOutcome(string outcome) =>
        _payments.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
}
