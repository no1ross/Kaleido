namespace Kaleido.Processor;

/// <summary>
/// Marks a process step whose input is the answers to an <see cref="InformationRequest"/>.
/// </summary>
/// <remarks>
/// <para>
/// A handler of an earlier step decides at runtime that questions must be answered and returns
/// <c>RequireInformation&lt;TNext&gt;(request)</c>, where <c>TNext</c> implements this
/// interface. The process waits in <c>AwaitingInformation</c>; the client submits the answers
/// as this step's payload.
/// </para>
/// <para>
/// An information step declares exactly these three properties and nothing else; startup fails
/// with <c>pro_invalid_registration</c> otherwise. Known inputs belong on ordinary steps as
/// properties; runtime-decided questions belong in information requests.
/// </para>
/// <example>
/// <code>
/// [ProcessStep(DisplayName = "Out-of-network responses", Description = "…")]
/// public sealed record CaptureOutOfNetworkResponseStep : IInformationStep
/// {
///     public required string InformationRequestId { get; init; }
///     public required string ExchangeId { get; init; }
///     public IReadOnlyList&lt;InformationResponseItem&gt; Items { get; init; } = [];
/// }
/// </code>
/// </example>
/// </remarks>
public interface IInformationStep : IProcessStep
{
    /// <summary>The <see cref="InformationRequest.InformationRequestId"/> being answered.</summary>
    string InformationRequestId { get; init; }

    /// <summary>
    /// The <see cref="InformationRequest.ExchangeId"/> of the pending request being answered, as it was
    /// received with the questions. Answers carrying another exchange's id are stale and rejected
    /// (<c>pro_information_response_stale</c>) before any handler runs (#442).
    /// </summary>
    string ExchangeId { get; init; }

    /// <summary>The answers, one entry per answered item.</summary>
    IReadOnlyList<InformationResponseItem> Items { get; init; }
}
