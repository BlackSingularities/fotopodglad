using System.Windows.Controls;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Xunit;

namespace Fotopodglad.Tests.Services;

public class LocalizationServiceTests
{
    [Fact]
    public void Apply_TranslatesToEnglishWithoutThrowing() => RunOnStaThread(() =>
    {
        var panel = new StackPanel();
        var label = new TextBlock { Text = "Ustawienia" };
        panel.Children.Add(label);

        LocalizationService.Apply(panel, new AppSettings { Language = LanguageMode.English });

        Assert.Equal("Settings", label.Text);
    });

    [Fact]
    public void Apply_TranslatesBackToPolishWithoutThrowing() => RunOnStaThread(() =>
    {
        var panel = new StackPanel();
        var label = new TextBlock { Text = "Settings" };
        panel.Children.Add(label);

        LocalizationService.Apply(panel, new AppSettings { Language = LanguageMode.Polish });

        Assert.Equal("Ustawienia", label.Text);
    });

    [Fact]
    public void EnglishTranslationsAreUnique()
    {
        // Słownik angielski → polski jest budowany z wartości, więc powtórzone tłumaczenie
        // oznaczałoby dwa polskie napisy nierozróżnialne po przełączeniu języka.
        var duplicates = LocalizationService.Translations
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    // Kontrolki WPF wymagają wątku STA, którego runner xUnit domyślnie nie zapewnia.
    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Exception("Test na wątku STA zakończył się błędem.", failure);
        }
    }
}
