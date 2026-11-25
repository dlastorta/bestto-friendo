import { AuthenticationError, csrfHeaders } from '../identity/api'

export type DeckSummary = { id: string; gameId: string; name: string; revision: number; version: number; isArchived: boolean }
export type DeckRevision = { number: number; name: string; listText: string; factionIds: string[] }
export type Deck = { id: string; gameId: string; isArchived: boolean; version: number; current: DeckRevision }

export async function deckRequest<T>(path = '', method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`/api/decks${path}`, {
    method, credentials: 'same-origin', cache: 'no-store',
    headers: method === 'GET' ? { Accept: 'application/json' } : await csrfHeaders(),
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  if (response.status === 401) throw new AuthenticationError()
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as { message?: string; errors?: Record<string, string[]> }
    throw new Error(error.message ?? Object.values(error.errors ?? {}).flat()[0] ?? 'No se pudo completar la operación.')
  }
  return response.json() as Promise<T>
}
