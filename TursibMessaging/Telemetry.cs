using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TursibMessaging;

/// <summary>
/// Urmele și metricile drumului unei poziții (OpenTelemetry, prin API-urile .NET <see cref="ActivitySource"/> și
/// <see cref="Meter"/>; SDK-ul OpenTelemetry din fiecare serviciu le exportă prin OTLP). Contextul de urmărire trece
/// prin antetele Kafka (W3C traceparent), deci o poziție se vede ca o singură urmă de la ingestie până la hartă.
/// </summary>
public static class PipelineTelemetry
{
    public const string Name = "Tursib.Pipeline";

    public static readonly ActivitySource Source = new(Name, "1.0");
    public static readonly Meter Meter = new(Name, "1.0");

    /// <summary>Mesaje de poziție primite, pe etape (ingestion / backend) și transport (mqtt / kafka / http).</summary>
    public static readonly Counter<long> Received =
        Meter.CreateCounter<long>("tursib.positions.received", "{position}", "Mesaje de poziție primite");
    public static readonly Counter<long> Rejected =
        Meter.CreateCounter<long>("tursib.positions.rejected", "{position}", "Mesaje respinse la validare, pe motiv");
    public static readonly Counter<long> Accepted =
        Meter.CreateCounter<long>("tursib.positions.accepted", "{position}", "Poziții acceptate (valide și mai noi)");
    /// <summary>Vârsta poziției la procesare: acum minus momentul raportat de dispozitiv (secunde).</summary>
    public static readonly Histogram<double> Age =
        Meter.CreateHistogram<double>("tursib.positions.age", "s", "Vârsta poziției la procesare");

    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    /// <summary>Scrie contextul activității curente în antetele unui mesaj (ex. Kafka).</summary>
    public static void Inject(Activity? activity, Action<string, string> setHeader)
    {
        if (activity == null) return;
        setHeader(TraceParentHeader, activity.Id!);
        if (!string.IsNullOrEmpty(activity.TraceStateString)) setHeader(TraceStateHeader, activity.TraceStateString!);
    }

    /// <summary>Citește contextul dintr-un mesaj; <c>default</c> dacă lipsește sau e invalid.</summary>
    public static ActivityContext Extract(Func<string, string?> getHeader)
    {
        var parent = getHeader(TraceParentHeader);
        return parent != null && ActivityContext.TryParse(parent, getHeader(TraceStateHeader), out var context)
            ? context
            : default;
    }

    /// <summary>
    /// Eșantionarea urmelor: la mii de poziții pe secundă, urmărirea fiecăreia costă (măsurat: Jaeger ajunge la ~4 GB).
    /// Raportul vine din <c>OTEL_TRACES_SAMPLER_ARG</c> (0–1, implicit 1). Decizia e luată la începutul urmei și respectată
    /// de toți (ParentBased), deci o urmă e fie completă, fie lipsește cu totul.
    /// </summary>
    public static OpenTelemetry.Trace.Sampler Sampler(string? ratio) =>
        new OpenTelemetry.Trace.ParentBasedSampler(new OpenTelemetry.Trace.TraceIdRatioBasedSampler(
            double.TryParse(ratio, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)
                ? Math.Clamp(r, 0, 1) : 1.0));

    /// <summary>Vârsta unei poziții cu timestamp-ul dat (secunde Unix).</summary>
    public static void RecordAge(long timestampSeconds, string stage, DateTimeOffset now)
    {
        if (timestampSeconds <= 0) return;
        Age.Record(Math.Max(0, now.ToUnixTimeSeconds() - timestampSeconds), new KeyValuePair<string, object?>("stage", stage));
    }
}
