// Due dates are calendar dates. The API returns "yyyy-MM-ddT00:00:00" (no offset), and the UI only ever
// works with the "yyyy-MM-dd" part, so time zones can never shift a task to the previous/next day.

export function toDateOnly(isoDateTime: string): string {
  return isoDateTime.slice(0, 10);
}

export function todayDateOnly(now: Date = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}
