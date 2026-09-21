using Kaleido.Process;
using Kaleido.Samples.PriorAuth.Configuration;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.CodeSet;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Messages;

public static class RadiologyProcessMessages
{
    public static ProcessMessage MemberNotFound(
        Guid memberId,
        Guid memberEnrollmentId) =>
        new()
        {
            Code = "MEMBER_NOT_FOUND",
            Type = MessageType.Error,
            Message = $"No member details were found for member '{memberId}' and enrollment '{memberEnrollmentId}'."
        };

    public static ProcessMessage CoverageNotYetEffective(
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        DateOnly effectiveDate) =>
        new()
        {
            Code = "COVERAGE_NOT_YET_EFFECTIVE",
            Type = MessageType.Error,
            Message = $"Enrollment '{memberEnrollmentId}' is not effective for date of service '{dateOfService:yyyy-MM-dd}'. Coverage starts on '{effectiveDate:yyyy-MM-dd}'."
        };

    public static ProcessMessage CoverageTerminated(
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        DateOnly terminationDate) =>
        new()
        {
            Code = "COVERAGE_TERMINATED",
            Type = MessageType.Error,
            Message = $"Enrollment '{memberEnrollmentId}' is not effective for date of service '{dateOfService:yyyy-MM-dd}'. Coverage ended on '{terminationDate:yyyy-MM-dd}'."
        };

    public static ProcessMessage ProcedureCodeNotFound(
        ProcedureCodeSystem codeSystem,
        string codeValue) =>
        new()
        {
            Code = "PROCEDURE_CODE_NOT_FOUND",
            Type = MessageType.Error,
            Message = $"No procedure code was found for '{codeSystem}:{codeValue}'."
        };

    public static ProcessMessage ProcedureCodeUpdated(
        ProcedureCodeSystem originalCodeSystem,
        string originalCodeValue,
        ProcedureCodeSystem updatedCodeSystem,
        string updatedCodeValue) =>
        new()
        {
            Code = "PROCEDURE_CODE_UPDATED",
            Type = MessageType.Information,
            Message = $"Requested service code was updated from '{originalCodeSystem}:{originalCodeValue}' to '{updatedCodeSystem}:{updatedCodeValue}' based on the selected MRI details."
        };

    public static ProcessMessage QueryableRequestFailed(
        string code,
        string message) =>
        new()
        {
            Code = code,
            Type = MessageType.Error,
            Message = message
        };

    public static ProcessMessage MixedRequestedServiceModalitiesNotAllowed(
        ProcedureModality existingModality,
        ProcedureModality requestedModality) =>
        new()
        {
            Code = "MIXED_REQUESTED_SERVICE_MODALITIES_NOT_ALLOWED",
            Type = MessageType.Error,
            Message = $"A prior authorization request cannot contain both '{existingModality}' and '{requestedModality}' services. Add only additional '{existingModality}' services to this request."
        };

    public static ProcessMessage DuplicateRequestedServiceNotAllowed(
        ProcedureCodeSystem codeSystem,
        string codeValue) =>
        new()
        {
            Code = "DUPLICATE_REQUESTED_SERVICE_NOT_ALLOWED",
            Type = MessageType.Error,
            Message = $"The requested service '{codeSystem}:{codeValue}' has already been added to this prior authorization request."
        };

    public static ProcessMessage RequestedServiceNotFound(
        Guid priorAuthorizationRequestedServiceId) =>
        new()
        {
            Code = "REQUESTED_SERVICE_NOT_FOUND",
            Type = MessageType.Warning,
            Message = $"Requested service '{priorAuthorizationRequestedServiceId}' was not found and may already have been removed."
        };

    public static ProcessMessage MemberInfoNotProvided() =>
        new()
        {
            Code = "MEMBER_INFO_NOT_PROVIDED",
            Type = MessageType.Information,
            Message = "Member information was not provided. Member capture will be required before proceeding."
        };

    public static ProcessMessage PriorAuthorizationNotFound(
        Guid processId) =>
        new()
        {
            Code = "PRIOR_AUTHORIZATION_NOT_FOUND",
            Type = MessageType.Error,
            Message = $"No prior authorization was found for process '{processId}'. Ensure the radiology intake has been started before capturing member information."
        };

    public static ProcessMessage ModalityNotSupported(
        ProcedureCodeSystem codeSystem,
        string codeValue,
        ProcedureModality modality) =>
        new()
        {
            Code = "MODALITY_NOT_SUPPORTED",
            Type = MessageType.Error,
            Message = $"Procedure code '{codeSystem}:{codeValue}' has modality '{modality}' which is not supported by the Radiology processor."
        };
}
