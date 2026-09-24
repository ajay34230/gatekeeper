/**
 * Precise Year / Month / Days / Hours / Minutes time breakdown calculations
 * for Client-Side Activity Monitoring and Gatekeeper records.
 */

export interface DurationBreakdown {
  years: number;
  months: number;
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  totalMs: number;
  formatted: string; // e.g. "0y 0m 2d 5h 32m" or "2d 5h 32m"
  humanReadable: string; // e.g. "2 days, 5 hours, 32 mins"
}

export function calculatePreciseBreakdown(startMs: number, endMs: number = Date.now()): DurationBreakdown {
  const diffMs = Math.max(0, endMs - startMs);

  const startDate = new Date(startMs);
  const endDate = new Date(endMs);

  let years = endDate.getFullYear() - startDate.getFullYear();
  let months = endDate.getMonth() - startDate.getMonth();
  let days = endDate.getDate() - startDate.getDate();
  let hours = endDate.getHours() - startDate.getHours();
  let minutes = endDate.getMinutes() - startDate.getMinutes();
  let seconds = endDate.getSeconds() - startDate.getSeconds();

  if (seconds < 0) {
    minutes -= 1;
    seconds += 60;
  }
  if (minutes < 0) {
    hours -= 1;
    minutes += 60;
  }
  if (hours < 0) {
    days -= 1;
    hours += 24;
  }
  if (days < 0) {
    months -= 1;
    const prevMonthLastDay = new Date(endDate.getFullYear(), endDate.getMonth(), 0).getDate();
    days += prevMonthLastDay;
  }
  if (months < 0) {
    years -= 1;
    months += 12;
  }
  if (years < 0) {
    years = 0;
    months = 0;
    days = 0;
    hours = 0;
    minutes = 0;
    seconds = 0;
  }

  const parts: string[] = [];
  if (years > 0) parts.push(`${years}y`);
  if (months > 0 || years > 0) parts.push(`${months}m`);
  if (days > 0 || months > 0 || years > 0) parts.push(`${days}d`);
  parts.push(`${hours}h`);
  parts.push(`${minutes}m`);

  const humanParts: string[] = [];
  if (years > 0) humanParts.push(`${years} ${years === 1 ? 'year' : 'years'}`);
  if (months > 0) humanParts.push(`${months} ${months === 1 ? 'month' : 'months'}`);
  if (days > 0) humanParts.push(`${days} ${days === 1 ? 'day' : 'days'}`);
  if (hours > 0) humanParts.push(`${hours} ${hours === 1 ? 'hour' : 'hours'}`);
  if (minutes > 0 || humanParts.length === 0) humanParts.push(`${minutes} ${minutes === 1 ? 'min' : 'mins'}`);

  return {
    years,
    months,
    days,
    hours,
    minutes,
    seconds,
    totalMs: diffMs,
    formatted: parts.join(' '),
    humanReadable: humanParts.join(', '),
  };
}

export function calculateDuration(startTimestamp?: number, endTimestamp?: number): string {
  if (!startTimestamp) return '0m';
  const breakdown = calculatePreciseBreakdown(startTimestamp, endTimestamp || Date.now());
  return breakdown.formatted;
}

export function formatTimeBeforeVisit(previousMs?: number, currentMs: number = Date.now()): string {
  if (!previousMs || previousMs <= 0 || previousMs >= currentMs) {
    return 'First recorded entry';
  }
  const breakdown = calculatePreciseBreakdown(previousMs, currentMs);
  return `${breakdown.humanReadable} ago`;
}
