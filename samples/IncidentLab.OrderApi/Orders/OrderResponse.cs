namespace IncidentLab.OrderApi.Orders;

public sealed record OrderResponse(long Id, string Status, decimal Total, string Currency);
