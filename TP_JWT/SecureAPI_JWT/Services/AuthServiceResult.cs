using SecureAPI_JWT.Models;

namespace SecureAPI_JWT.Services;

/// <summary>Statuts métier d'une opération d'authentification.</summary>
public enum AuthServiceStatus
{
    Success,
    InvalidRequest,
    Conflict,
    Unauthorized,
    Failed
}

/// <summary>
/// Résultat explicite d'une opération d'authentification. Le statut permet au
/// contrôleur de renvoyer le code HTTP adapté sans interpréter un message.
/// </summary>
public sealed class AuthServiceResult
{
    private AuthServiceResult(AuthServiceStatus status, AuthModel model)
    {
        Status = status;
        Model = model;
    }

    public AuthServiceStatus Status { get; }

    public AuthModel Model { get; }

    public static AuthServiceResult Success(AuthModel model) =>
        new(AuthServiceStatus.Success, model);

    public static AuthServiceResult InvalidRequest(string message) =>
        new(AuthServiceStatus.InvalidRequest, Failure(message));

    public static AuthServiceResult Conflict(string message) =>
        new(AuthServiceStatus.Conflict, Failure(message));

    public static AuthServiceResult Unauthorized(string message) =>
        new(AuthServiceStatus.Unauthorized, Failure(message));

    public static AuthServiceResult Failed(string message) =>
        new(AuthServiceStatus.Failed, Failure(message));

    private static AuthModel Failure(string message) => new()
    {
        IsAuthenticated = false,
        Message = message
    };
}

/// <summary>Statut métier d'une attribution de rôle.</summary>
public enum RoleAssignmentStatus
{
    Success,
    InvalidRequest,
    NotFound,
    Failed
}

/// <summary>Résultat d'une attribution de rôle.</summary>
public sealed class RoleAssignmentResult
{
    private RoleAssignmentResult(RoleAssignmentStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public RoleAssignmentStatus Status { get; }

    public string Message { get; }

    public static RoleAssignmentResult Success() =>
        new(RoleAssignmentStatus.Success, string.Empty);

    public static RoleAssignmentResult InvalidRequest(string message) =>
        new(RoleAssignmentStatus.InvalidRequest, message);

    public static RoleAssignmentResult NotFound(string message) =>
        new(RoleAssignmentStatus.NotFound, message);

    public static RoleAssignmentResult Failed(string message) =>
        new(RoleAssignmentStatus.Failed, message);
}
