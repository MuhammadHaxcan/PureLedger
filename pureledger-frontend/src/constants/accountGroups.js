export const ACCOUNT_GROUP = Object.freeze({
    ACCOUNT: 1,
    PRODUCT: 2,
    EXPENSE: 3,
});

export const isAccountGroup = (value, group) => Number(value) === group;
