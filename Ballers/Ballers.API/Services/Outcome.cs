using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Services
{
    public enum OutcomeStatus { Ok, NotFound, Invalid, Conflict }

    /// <summary>
    /// What a service write came to, without the service knowing about HTTP.
    /// Controllers turn it into a response with <see cref="OutcomeResults"/>.
    /// </summary>
    public sealed class Outcome
    {
        public OutcomeStatus Status { get; }
        public string? Error { get; }
        public bool Succeeded => Status == OutcomeStatus.Ok;

        private Outcome(OutcomeStatus status, string? error)
        {
            Status = status;
            Error = error;
        }

        public static readonly Outcome Ok = new(OutcomeStatus.Ok, null);
        public static Outcome NotFound(string? why = null) => new(OutcomeStatus.NotFound, why);
        // The request itself is wrong: a missing name, a file that is too big.
        public static Outcome Invalid(string why) => new(OutcomeStatus.Invalid, why);
        // The request is fine but clashes with what is already there.
        public static Outcome Conflict(string why) => new(OutcomeStatus.Conflict, why);
    }

    /// <summary>An <see cref="Outcome"/> that carries a value when it succeeds.</summary>
    public sealed class Outcome<T>
    {
        public Outcome Result { get; }
        public T? Value { get; }
        public bool Succeeded => Result.Succeeded;

        private Outcome(Outcome result, T? value)
        {
            Result = result;
            Value = value;
        }

        public static implicit operator Outcome<T>(T value) => new(Outcome.Ok, value);

        public static implicit operator Outcome<T>(Outcome failure)
            => failure.Succeeded
                ? throw new ArgumentException("A successful outcome needs a value.", nameof(failure))
                : new(failure, default);
    }

    public static class OutcomeResults
    {
        public static IActionResult ToActionResult(this ControllerBase c, Outcome o, Func<IActionResult>? whenOk = null)
            => o.Status switch
            {
                OutcomeStatus.Ok => whenOk?.Invoke() ?? c.Ok(),
                OutcomeStatus.NotFound => o.Error is null ? c.NotFound() : c.NotFound(o.Error),
                OutcomeStatus.Invalid => c.BadRequest(o.Error),
                _ => c.Conflict(o.Error)
            };

        public static IActionResult ToActionResult<T>(this ControllerBase c, Outcome<T> o, Func<T, object?>? shape = null)
            => c.ToActionResult(o.Result, () => c.Ok(shape is null ? o.Value : shape(o.Value!)));
    }
}
