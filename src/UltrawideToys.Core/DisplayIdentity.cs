namespace UltrawideToys.Core;

internal sealed record DisplayIdentity(string? FriendlyName, string DevicePath, ushort EdidManufacturerId, ushort EdidProductCodeId, uint ConnectorInstance);

