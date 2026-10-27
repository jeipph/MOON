namespace Moon.Core.Models;

public enum AppTheme
{
    /// <summary>Sigue el modo claro/oscuro de Windows.</summary>
    System,
    Dark,
    Light,
}

/// <summary>Opciones de MOON. Se guardan en <c>%APPDATA%\MOON\settings.json</c>.</summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;
}
