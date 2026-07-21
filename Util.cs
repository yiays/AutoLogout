using System;
using Avalonia.Styling;

namespace AutoLogout;

public sealed class ThemeVariantObserver : IObserver<ThemeVariant>
{
    private readonly Action<ThemeVariant> _onNext;

    public ThemeVariantObserver(Action<ThemeVariant> onNext)
    {
        _onNext = onNext;
    }

    public void OnCompleted() { }

    public void OnError(Exception error) { }

    public void OnNext(ThemeVariant value) => _onNext(value);
}