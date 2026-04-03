namespace HouseholdBudgetMate.Abstractions.Enums;

public enum ServiceResultStatusCode
{
    /// <summary>
    /// The 200 OK status code means that the request was processed successfully.
    /// </summary>
    Ok = 200,
    
    /// <summary>
    /// The 201 Created status code means that the request was successfully fulfilled and resulted in one or possibly multiple new resources being created.
    /// </summary>
    Created = 201,
    
    /// <summary>
    /// The 202 Accepted status code means that the request has been accepted for processing, but the processing has not been finished yet.
    /// </summary>
    Accepted = 202,
    
    /// <summary>
    /// The 204 No Content status code means that while the server has successfully fulfilled the request, there is no available content for this request.
    /// </summary>
    NoContent = 204,

    /// <summary>
    /// The 400 Bad Request status code means that the server could not understand the request because of invalid syntax.
    /// </summary>
    BadRequest = 400,

    /// <summary>
    /// The 401 Unauthorized status code means that the request has not been applied because the server requires user authentication.
    /// </summary>
    Unauthorized = 401,

    /// <summary>
    /// The 402 Payment Required code means that payment is required before the client can access the requested resource.
    /// </summary>
    PaymentRequired = 402,

    /// <summary>
    /// The 403 Forbidden status code means that the client request has been rejected because the client does not have rights to access the content.
    /// </summary>
    /// <remarks>
    /// Unlike a 401 error, the client's identity is known to the server, but since they are not authorized to view the content, giving the proper response is rejected by the server.
    /// </remarks>
    Forbidden = 403,

    /// <summary>
    /// The 404 Not Found status code means that the server either did not find a current representation for the requested resource or is trying to hide its existence from an unauthorized client.
    /// </summary>
    NotFound = 404,

    /// <summary>
    /// The 409 Conflict status code means that the request could not be fulfilled due to a conflict with the current state of the target resource and is used in situations where the user might be able to resubmit the request after resolving the conflict.
    /// </summary>
    Conflict = 409,

    /// <summary>
    /// The 410 Gone status code means that the target resource has been deleted and the condition seems to be permanent. 
    /// </summary>
    Gone = 410,

    /// <summary>
    /// The 413 Payload Too Large status code means the server refuses to process the request because the request payload is larger than the server is able or willing to process.
    /// </summary>
    PayloadTooLarge = 413,

    /// <summary>
    /// The 416 Range Not Satisfiable status code means that the range specified in the request can't be fulfilled.
    /// </summary>
    OutOfRange = 416,
        
    /// <summary>
    /// The 422 Unprocessable Entity status code means that while the request was well-formed, the server was unable to follow it, due to semantic errors.
    /// </summary>
    ValidationError = 422,

    /// <summary>
    /// The 423 Locked status code means that the resource that is being accessed is locked.
    /// </summary>
    Locked = 423,

    /// <summary>
    /// The 424 Failed Dependency status code means that the request failed due to the failure of a some depending service or API.
    /// </summary>
    FailedDependency = 424,

    /// <summary>
    /// The 429 Too Many Requests response code means that in the given time, the user has sent too many requests.
    /// </summary>
    TooManyRequests = 429,
        
    /// <summary>
    /// The 499 Client Closed Request response code means that client wasn't waiting for the request to finish and disconnected. 
    /// </summary>
    Cancelled = 499,

    /// <summary>
    /// The 500 Internal Server Error status code means that the server has encountered a situation that it does not know how to handle the request.
    /// </summary>
    Internal = 500,

    /// <summary>
    /// The 501 Not Implemented response code means that the request can not be handled because it is not supported by the server.
    /// </summary>
    Unimplemented = 501,

    /// <summary>
    /// The 503 Service Unavailable response code means that the server is currently not ready to handle the request. This is a common occurrence when the server is down for maintenance or is overloaded.
    /// </summary>
    Unavailable = 503,

    /// <summary>
    /// The 504 Gateway Timeout response code means that the server could not process request in given time.
    /// </summary>
    Timeout = 504
}