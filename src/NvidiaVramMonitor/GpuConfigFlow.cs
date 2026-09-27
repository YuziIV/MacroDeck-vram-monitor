using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;

namespace NvidiaVramMonitor;

public sealed class GpuConfigFlow : IConfigFlow
{
	public const string IndexField = "gpuIndex";
	private const string StepId = "gpu";

	public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
		=> Task.FromResult(ConfigFlowResult.Step(BuildStep()));

	public Task<ConfigFlowResult> SubmitAsync(string stepId, IReadOnlyDictionary<string, object?> input,
		IConfigFlowContext context, CancellationToken cancellationToken)
	{
		if (stepId != StepId)
		{
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), Strings.ConfigFlow.UnknownStep()));
		}

		if (!TryGetIndex(input.GetValueOrDefault(IndexField)?.ToString(), out var index))
		{
			var error = Strings.ConfigFlow.InvalidIndex();
			return Task.FromResult(ConfigFlowResult.Error(BuildStep(), error,
				new Dictionary<string, LocalizedText> { [IndexField] = error }));
		}

		return Task.FromResult(ConfigFlowResult.Complete($"NVIDIA GPU {index}"));
	}

	public static bool TryGetIndex(string? value, out int index) =>
		int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;

	private static ConfigFlowStep BuildStep() => new()
	{
		StepId = StepId,
		Title = Strings.ConfigFlow.Title(),
		Description = Strings.ConfigFlow.Description(),
		Fields = [ActionParameter.Text(IndexField, label: Strings.ConfigFlow.Index.Label(),
			defaultValue: "0", required: true)]
	};
}
