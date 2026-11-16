using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CompanyApi.Api.Errors;

/// <summary>Maps [ApiController] model-state failures to the uniform <see cref="ErrorResponse"/>.</summary>
public static class ValidationErrorFactory
{
    public const string MalformedBodyMessage = "The request body is empty or is not valid JSON.";

    /// <summary>Controllers name their [FromBody] parameter "request" so body errors can be recognized.</summary>
    public const string BodyParameterName = "request";

    public static IActionResult Create(ActionContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ValidationErrorFactory));

        var response = Build(context.ModelState);
        logger.LogWarning("Request validation failed for {Method} {Path}: {Reasons}",
            context.HttpContext.Request.Method, context.HttpContext.Request.Path, string.Join(" ", response.Errors));

        return new BadRequestObjectResult(response);
    }

    private static ErrorResponse Build(ModelStateDictionary modelState)
    {
        var invalid = modelState.Where(kv => kv.Value?.Errors.Count > 0).ToList();

        // System.Text.Json failures surface under "$..." keys; a missing body under "" or the body parameter name.
        var bodyProblem = invalid.Any(kv => kv.Key.StartsWith('$') || kv.Key.Length == 0 || kv.Key == BodyParameterName);
        if (bodyProblem)
        {
            return ErrorResponse.From(MalformedBodyMessage);
        }

        var errors = invalid
            .SelectMany(kv => kv.Value!.Errors.Select(e => Describe(kv.Key, e)))
            .Distinct()
            .ToList();

        return errors.Count == 0
            ? ErrorResponse.From("The request is invalid.")
            : new ErrorResponse(errors[0], errors);
    }

    private static string Describe(string key, ModelError error) =>
        string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? $"The value for '{key}' is invalid."
            : error.ErrorMessage;
}
