// Calendar dates (entry, due, report ranges, export file names) follow the business time zone, the same
// zone the server uses (Business:TimeZoneId, default Asia/Karachi). Building them from
// `new Date().toISOString()` gives the UTC date, which is the previous day between midnight and 05:00 in
// Pakistan. Override with VITE_BUSINESS_TIME_ZONE only if the server's Business:TimeZoneId is changed too.
export const BUSINESS_TIME_ZONE = import.meta.env?.VITE_BUSINESS_TIME_ZONE || 'Asia/Karachi';

const partsFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: BUSINESS_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Today's date as YYYY-MM-DD in the business time zone. */
export const todayString = (now = new Date()) => {
  const parts = Object.fromEntries(partsFormatter.formatToParts(now).map(part => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day}`;
};

const toUtcDate = (dateString) => {
  const [year, month, day] = String(dateString).split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
};

const fromUtcDate = (date) => date.toISOString().slice(0, 10);

/** Adds whole days to a YYYY-MM-DD string without any time-zone drift. */
export const addDays = (dateString, days) => {
  const date = toUtcDate(dateString);
  date.setUTCDate(date.getUTCDate() + days);
  return fromUtcDate(date);
};

/** Adds whole months to a YYYY-MM-DD string, clamping to the end of a shorter month. */
export const addMonths = (dateString, months) => {
  const date = toUtcDate(dateString);
  const day = date.getUTCDate();
  date.setUTCDate(1);
  date.setUTCMonth(date.getUTCMonth() + months);
  const lastDay = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
  date.setUTCDate(Math.min(day, lastDay));
  return fromUtcDate(date);
};
