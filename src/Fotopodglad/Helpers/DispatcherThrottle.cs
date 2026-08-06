using System.Windows.Threading;

namespace Fotopodglad.Helpers;

/// <summary>
/// Ogranicza częstotliwość wykonywania akcji na wątku UI do co najwyżej raz na zadany interwał —
/// przydatne przy seriach zdjęć nadchodzących w krótkich odstępach (np. seria zdjęć w trybie ciągłym),
/// żeby nie przeliczać layoutu siatki przy każdym pojedynczym pliku z osobna.
/// </summary>
public sealed class DispatcherThrottle
{
    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _interval;
    private readonly Action _action;
    private readonly DispatcherTimer _timer;
    private bool _pending;

    public DispatcherThrottle(Dispatcher dispatcher, TimeSpan interval, Action action)
    {
        _dispatcher = dispatcher;
        _interval = interval;
        _action = action;
        _timer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher) { Interval = interval };
        _timer.Tick += (_, _) => Flush();
    }

    public void Request()
    {
        if (_pending)
        {
            return;
        }

        _pending = true;
        _timer.Start();
    }

    private void Flush()
    {
        _timer.Stop();
        if (!_pending)
        {
            return;
        }

        _pending = false;
        _action();
    }
}
