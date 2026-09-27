using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using MacroDeck.Sdk.Variables;
using Serilog;
using System.Diagnostics;
using System.Globalization;

namespace NvidiaVramMonitor;

/// <summary>
/// The plugin's one integration. It declares a single example action: add more to <see cref="Actions"/>,
/// and opt into a capability by implementing its interface here (<c>IVariableProvider</c>,
/// <c>IEventProvider</c>, <c>IConfigFlowProvider</c>, and so on).
/// </summary>
public sealed class PluginIntegration : IPluginIntegration, IVariableProvider, IConfigFlowProvider, IDisposable
{
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _refreshLock = new(1, 1);
	private VramReading? _cachedReading;
	private int _gpuIndex;

	public IConfigFlow CreateConfigFlow() => new GpuConfigFlow();
	public bool AllowsMultipleConfigurations => false;

	public IReadOnlyList<VariableDefinition> Variables { get; } =
	[
		VariableDefinition.Eager("vram-used-percent", VariableType.Numeric, decimalPlaces: 1, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "vram-used-percent", Unit = "%" },
		VariableDefinition.Eager("vram-used-mib", VariableType.Numeric, decimalPlaces: 0, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "vram-used-mib", Unit = "MiB" },
		VariableDefinition.Eager("vram-total-mib", VariableType.Numeric, decimalPlaces: 0, refreshInterval: TimeSpan.FromSeconds(1)) with { Id = "vram-total-mib", Unit = "MiB" },
	];

	public IReadOnlyList<IActionDefinition> Actions { get; } = [];

	// Built by DI, so anything the container knows can be taken here: IHttpClientFactory, IOptions<T>,
	// PluginMetadata, IPluginCatalogNotifier.
	public PluginIntegration(ILogger logger)
	{
		_logger = logger.ForContext<PluginIntegration>();
	}

	public async Task InitializeAsync(IIntegrationContext context)
	{
		_gpuIndex = 0;
		var entries = await context.Config.GetEntriesAsync();
		if (entries.Count > 0)
		{
			var selected = await context.Config.GetStringAsync(entries[0].Id, GpuConfigFlow.IndexField);
			if (GpuConfigFlow.TryGetIndex(selected, out var index))
			{
				_gpuIndex = index;
			}
		}
		_cachedReading = null;
		_logger.Information("Initialized.");
	}

	public Task ShutdownAsync() => Task.CompletedTask;

	public void Dispose() => _refreshLock.Dispose();

	public async ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
	{
		var reading = await GetReadingAsync(cancellationToken);
		if (reading is null)
		{
			return VariableReading.Unavailable;
		}
		var current = reading.Value;

		return localId switch
		{
			"vram-used-percent" => VariableReading.Of(current.UsedMiB * 100d / current.TotalMiB),
			"vram-used-mib" => VariableReading.Of(current.UsedMiB),
			"vram-total-mib" => VariableReading.Of(current.TotalMiB),
			_ => VariableReading.Unavailable,
		};
	}

	private async Task<VramReading?> GetReadingAsync(CancellationToken cancellationToken)
	{
		if (_cachedReading is { } cached && DateTimeOffset.UtcNow - cached.ReadAt < TimeSpan.FromSeconds(1))
		{
			return cached;
		}

		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			if (_cachedReading is { } refreshed && DateTimeOffset.UtcNow - refreshed.ReadAt < TimeSpan.FromSeconds(1))
			{
				return refreshed;
			}

			Process? process;
			try
			{
				process = Process.Start(new ProcessStartInfo(FindNvidiaSmi())
				{
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					ArgumentList = { $"--id={_gpuIndex}", "--query-gpu=memory.used,memory.total", "--format=csv,noheader,nounits" },
				});
			}
			catch (System.ComponentModel.Win32Exception)
			{
				return null;
			}
			if (process is null)
			{
				return null;
			}
			using (process)
			{

				var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
				await process.WaitForExitAsync(cancellationToken);
				if (process.ExitCode != 0 || !TryParseReading(output, out var reading))
				{
					return null;
				}

				_cachedReading = reading with { ReadAt = DateTimeOffset.UtcNow };
				return _cachedReading;
			}
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	private static string FindNvidiaSmi()
	{
		if (!OperatingSystem.IsWindows()) return "nvidia-smi";

		// NVIDIA drivers do not always add NVSMI to PATH on Windows.
		var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		var candidates = new[]
		{
			Path.Combine(windowsDirectory, "System32", "nvidia-smi.exe"),
			Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
		};
		return candidates.FirstOrDefault(File.Exists) ?? "nvidia-smi";
	}

	private static bool TryParseReading(string output, out VramReading reading)
	{
		var values = output.Trim().Split(',', StringSplitOptions.TrimEntries);
		if (values.Length == 2 &&
			double.TryParse(values[0], CultureInfo.InvariantCulture, out var usedMiB) &&
			double.TryParse(values[1], CultureInfo.InvariantCulture, out var totalMiB) &&
			totalMiB > 0)
		{
			reading = new VramReading(usedMiB, totalMiB, DateTimeOffset.MinValue);
			return true;
		}

		reading = default;
		return false;
	}

	private readonly record struct VramReading(double UsedMiB, double TotalMiB, DateTimeOffset ReadAt);
}
