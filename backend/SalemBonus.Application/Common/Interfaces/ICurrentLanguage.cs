namespace SalemBonus.Application.Common.Interfaces;

/// <summary>Сұраныстың тілі (Accept-Language тақырыбынан). Қолдау көрсетілмесе қазақша.</summary>
public interface ICurrentLanguage
{
    AppLanguage Value { get; }
}

public enum AppLanguage
{
    Kk,
    Ru,
}
