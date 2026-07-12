using System.ComponentModel.DataAnnotations;

namespace HomeStock.Application.Common;

/// <summary>
/// Centralized DataAnnotations validation. Services call this at their boundary so validation
/// rules live on the input models and are enforced consistently regardless of caller (UI or API).
/// </summary>
public static class ModelValidator
{
    public static IReadOnlyList<string> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Invalid value.").ToList();
    }

    public static bool TryValidate(object model, out IReadOnlyList<string> errors)
    {
        errors = Validate(model);
        return errors.Count == 0;
    }
}
