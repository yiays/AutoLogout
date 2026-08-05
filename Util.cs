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

public class ConditionalValue<T>(Func<bool> condition, T value, bool optional=false)
{
  public Func<bool> Condition { get; set; } = condition;
  public T Value { get; set; } = value;
  public bool Optional = optional;
  public bool Cleared = false;
}