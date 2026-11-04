import type { TicketType } from '@/services/ticketsService'

// Tarife Tursib Sibiu în vigoare de la 1 aprilie 2025.
// Trebuie să corespundă cu TicketCatalog din TicketsController (backend-ul validează prețul).
export interface TicketOption {
  id: TicketType
  /** chei de traducere (useLanguage) */
  nameKey: string
  descKey: string
  price: number
  /** valabilitate în minute */
  validityMinutes: number
}

export const TICKET_CATALOG: readonly TicketOption[] = [
  { id: 'single', nameKey: 'ticketSingle', descKey: 'ticketSingleDesc', price: 3.5, validityMinutes: 60 },
  { id: 'daily', nameKey: 'ticketDaily', descKey: 'ticketDailyDesc', price: 7, validityMinutes: 60 * 24 },
  { id: 'weekly', nameKey: 'ticketWeekly', descKey: 'ticketWeeklyDesc', price: 24, validityMinutes: 60 * 24 * 7 },
  { id: 'monthly_nominal', nameKey: 'ticketMonthlyNominal', descKey: 'ticketMonthlyNominalDesc', price: 90, validityMinutes: 60 * 24 * 30 },
  { id: 'monthly_nonnominal', nameKey: 'ticketMonthlyNonNominal', descKey: 'ticketMonthlyNonNominalDesc', price: 126, validityMinutes: 60 * 24 * 30 }
]

export function ticketOption(type: TicketType | string | undefined): TicketOption {
  return TICKET_CATALOG.find(o => o.id === type) ?? TICKET_CATALOG[0]!
}

/** 3.5 → "3,50 lei" (format românesc) */
export function formatPrice(value: number): string {
  return `${value.toLocaleString('ro-RO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} lei`
}

/** Statusul real: un bilet „active” cu validUntil trecut e expirat. */
export function effectiveTicketStatus(ticket: { status: string; validUntil: string }, now = new Date()): string {
  if (ticket.status !== 'active') return ticket.status
  return new Date(ticket.validUntil) < now ? 'expired' : 'active'
}
