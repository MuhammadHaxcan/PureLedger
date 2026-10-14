// Availability contains the exact carrying value; the displayed unit rate is rounded.
export const transferCarryingValue = (item) => {
  const quantity = Number(item.quantity || 0);
  const available = Number(item.availableQuantity || 0);
  // Mirrors StockTransferService.PrepareDispatchAsync: carrying value applies only to positive stock with positive value.
  if (item.inventoryValue != null && available > 0 && Number(item.inventoryValue) > 0
    && Number(item.unitCost) === Number(item.suggestedUnitCost)) {
    const value = Number(item.inventoryValue);
    return quantity === available ? value : Math.round((value / available * quantity + Number.EPSILON) * 10000) / 10000;
  }
  return Math.round((quantity * Number(item.unitCost || 0) + Number.EPSILON) * 10000) / 10000;
};
