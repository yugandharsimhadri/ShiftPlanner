import { describe, expect, it } from 'vitest'
import { addMonths, daysInMonth, isWeekend, monthLabel, toIsoDate, weekdayLetter } from './dates'

describe('daysInMonth', () => {
  it('handles a 31-day month', () => {
    expect(daysInMonth(2026, 7)).toBe(31)
  })

  it('handles a 30-day month', () => {
    expect(daysInMonth(2026, 4)).toBe(30)
  })

  it('handles February in a leap year', () => {
    expect(daysInMonth(2028, 2)).toBe(29)
  })

  it('handles February in a non-leap year', () => {
    expect(daysInMonth(2026, 2)).toBe(28)
  })
})

describe('addMonths', () => {
  it('rolls over into the next year', () => {
    expect(addMonths(2026, 12, 1)).toEqual({ year: 2027, month: 1 })
  })

  it('rolls back into the previous year', () => {
    expect(addMonths(2026, 1, -1)).toEqual({ year: 2025, month: 12 })
  })

  it('handles a multi-month jump spanning a year boundary', () => {
    expect(addMonths(2026, 11, 4)).toEqual({ year: 2027, month: 3 })
  })

  it('is a no-op for a zero delta', () => {
    expect(addMonths(2026, 7, 0)).toEqual({ year: 2026, month: 7 })
  })
})

describe('toIsoDate', () => {
  it('zero-pads single-digit months and days', () => {
    expect(toIsoDate(2026, 7, 9)).toBe('2026-07-09')
  })

  it('leaves double-digit months and days alone', () => {
    expect(toIsoDate(2026, 12, 31)).toBe('2026-12-31')
  })
})

describe('monthLabel', () => {
  it('formats a short month name with the year', () => {
    expect(monthLabel(2026, 7)).toBe('Jul 2026')
  })
})

describe('isWeekend', () => {
  it('flags a Saturday', () => {
    // July 11, 2026 is a Saturday.
    expect(isWeekend(2026, 7, 11)).toBe(true)
  })

  it('flags a Sunday', () => {
    // July 12, 2026 is a Sunday.
    expect(isWeekend(2026, 7, 12)).toBe(true)
  })

  it('does not flag a weekday', () => {
    // July 9, 2026 is a Thursday.
    expect(isWeekend(2026, 7, 9)).toBe(false)
  })
})

describe('weekdayLetter', () => {
  it('returns the correct single-letter code for each day of a known week', () => {
    // July 5-11, 2026 is Sun..Sat.
    expect(weekdayLetter(2026, 7, 5)).toBe('S')
    expect(weekdayLetter(2026, 7, 6)).toBe('M')
    expect(weekdayLetter(2026, 7, 7)).toBe('T')
    expect(weekdayLetter(2026, 7, 8)).toBe('W')
    expect(weekdayLetter(2026, 7, 9)).toBe('T')
    expect(weekdayLetter(2026, 7, 10)).toBe('F')
    expect(weekdayLetter(2026, 7, 11)).toBe('S')
  })
})
