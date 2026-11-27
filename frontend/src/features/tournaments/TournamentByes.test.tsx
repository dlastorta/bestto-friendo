import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { TournamentByes } from './TournamentByes'

afterEach(() => vi.unstubAllGlobals())
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })

it('asigna un bye con CSRF y muestra puntos y auditoría recibidos del servidor', async () => {
  let assigned = false
  const bye = { id: 'bye', round: 2, playerId: 'a', points: 2.5, actorPlayerId: 'a', assignedAt: '2026-11-27T10:00:00Z' }
  const request = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response([{ playerId: 'a', displayName: 'Ana' }])
    if (init?.method === 'POST') { assigned = true; return response(bye) }
    return response(assigned ? [bye] : [])
  })
  vi.stubGlobal('fetch', request)
  render(<TournamentByes tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar byes' }))
  await screen.findByRole('option', { name: 'Ana' })
  fireEvent.change(screen.getByLabelText('Jugador del bye'), { target: { value: 'a' } })
  fireEvent.change(screen.getByLabelText('Ronda del bye'), { target: { value: '2' } })
  fireEvent.click(screen.getByRole('button', { name: 'Asignar bye' }))
  expect(await screen.findByText(/Ronda 2: Ana · Bye · 2.5 puntos/)).toBeInTheDocument()
  expect(screen.getByText(/Asignó Ana/)).toBeInTheDocument()
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/byes', expect.objectContaining({
    body: JSON.stringify({ round: 2, playerId: 'a' }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  }))
})

it('conserva la selección ante conflicto sin mostrar una asignación exitosa', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response([{ playerId: 'a', displayName: 'Ana' }])
    return init?.method === 'POST' ? response({ message: 'La ronda ya tiene un bye.' }, 409) : response([])
  }))
  render(<TournamentByes tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar byes' }))
  await screen.findByRole('option', { name: 'Ana' })
  fireEvent.change(screen.getByLabelText('Jugador del bye'), { target: { value: 'a' } })
  fireEvent.click(screen.getByRole('button', { name: 'Asignar bye' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('La ronda ya tiene un bye.')
  expect(screen.getByLabelText('Jugador del bye')).toHaveValue('a')
  expect(screen.queryByRole('listitem')).not.toBeInTheDocument()
})

it.each([401, 403])('cierra la vista privada al perder acceso (%s)', async status => {
  const lost = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => response({}, status)))
  render(<TournamentByes tournamentId="cup" onAccessLost={lost} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar byes' }))
  await waitFor(() => expect(lost).toHaveBeenCalledOnce())
})
