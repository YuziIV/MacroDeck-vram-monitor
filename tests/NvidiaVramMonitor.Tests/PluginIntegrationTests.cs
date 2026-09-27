using MacroDeck.Plugin.Testing;
using NUnit.Framework;
using Serilog;

namespace NvidiaVramMonitor.Tests;

/// <summary>
/// Behaviour tests through <see cref="PluginTestHarness"/>: the plugin's own capability handlers run,
/// but nothing crosses a socket. This is where you test what your integration does.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private static PluginTestHarness CreateHarness() =>
		PluginTestHarness.Create(builder => builder
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<PluginIntegration>());

	[Test]
	public async Task The_plugin_builds_and_initializes()
	{
		await using var harness = CreateHarness();

		Assert.DoesNotThrowAsync(harness.InitializeIntegrationsAsync);
	}

	public static void The_vram_variables_are_declared()
	{
		var integration = new PluginIntegration(new LoggerConfiguration().CreateLogger());

		Assert.That(integration.Variables.Select(definition => definition.Id), Is.EquivalentTo([
			"vram-used-percent",
			"vram-used-mib",
			"vram-total-mib",
		]));
	}

	[TestCase("0", true)]
	[TestCase("12", true)]
	[TestCase("-1", false)]
	[TestCase("2;rm -rf /", false)]
	[TestCase("1.5", false)]
	public void Gpu_index_must_be_a_nonnegative_integer(string input, bool valid)
	{
		Assert.That(GpuConfigFlow.TryGetIndex(input, out _), Is.EqualTo(valid));
	}
}

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.yussefabdelwahab.nvidia-smi-tool"));
	}

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True);
			Assert.That(text, Is.Not.Empty);
		}
	}
}
