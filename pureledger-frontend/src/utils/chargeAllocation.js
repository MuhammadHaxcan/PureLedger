const round4 = (value) => Math.round((Number(value) + Number.EPSILON) * 10000) / 10000;

export const calculateAllocations = (charge, selectedItems) => {
  const amount = round4(charge.amount);
  const selected = selectedItems;

  if (charge.allocationMethod === 'Manual') {
    return Object.fromEntries(selected.map((item) => [
      item.productAccountId,
      round4(charge.allocations[item.productAccountId] || 0),
    ]));
  }

  const weights = selected.map((item) => ({
    productAccountId: item.productAccountId,
    weight: charge.allocationMethod === 'Equal'
      ? 1
      : charge.allocationMethod === 'ByQuantity'
        ? item.quantity
        : charge.allocationMethod === 'ByRate'
          ? item.unitCost
          : item.baseValue,
  }));
  const totalWeight = weights.reduce((sum, item) => sum + item.weight, 0);
  if (!totalWeight || !amount) {
    return Object.fromEntries(weights.map((item) => [item.productAccountId, 0]));
  }

  // Whole 0.0001 units: floor every share, then give leftover units to the largest remainders
  // (ties: larger weight, then higher id) so shares are never negative and sum exactly to the charge.
  const totalUnits = Math.round(amount * 10000);
  const shares = weights.map((item) => {
    const exact = totalUnits * item.weight / totalWeight;
    const floor = Math.floor(exact + 1e-9);
    return { ...item, floor, fraction: Math.max(0, exact - floor) };
  });
  const leftover = totalUnits - shares.reduce((sum, item) => sum + item.floor, 0);
  const bonus = new Set([...shares]
    .sort((a, b) => b.fraction - a.fraction || b.weight - a.weight || b.productAccountId - a.productAccountId)
    .slice(0, Math.max(0, leftover))
    .map((item) => item.productAccountId));
  const result = Object.fromEntries(shares.map((item) => [
    item.productAccountId,
    (item.floor + (bonus.has(item.productAccountId) ? 1 : 0)) / 10000,
  ]));
  return result;
};
