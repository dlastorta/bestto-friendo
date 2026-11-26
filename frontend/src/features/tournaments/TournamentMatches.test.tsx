import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { TournamentMatches } from './TournamentMatches'

afterEach(() => vi.unstubAllGlobals())
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })
const match = { id: 'match', round: 1, playerOneId: 'one', playerTwoId: 'two', version: 0, result: null }
const players = [{ playerId: 'one', displayName: 'Ana' }, { playerId: 'two', displayName: 'Bruno' }]

it('crea una partida y corrige su resultado con versiones, CSRF e historial', async () => {
  let current: Record<string, unknown> = match
  const request = vi.fn(async (url: string, init?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response(players)
    if (url.endsWith('/result')) {
      const body = JSON.parse(init!.body as string)
      const result = { number: Number(current.version) + 1, ...body, actorPlayerId: 'one', recordedAt: '2026-11-26T10:00:00Z', playerOnePoints: 2, playerTwoPoints: 2 }
      current = { ...match, version: result.number, result, history: [result] }
      return response(current)
    }
    if (url.endsWith('/match') || init?.method === 'POST') return response(current)
    return response([current])
  })
  vi.stubGlobal('fetch', request)
  render(<TournamentMatches tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar partidas' }))
  await screen.findByRole('button', { name: 'Ronda 1: Ana vs Bruno' })
  fireEvent.change(screen.getByLabelText('Jugador 1'), { target: { value: 'one' } })
  fireEvent.change(screen.getByLabelText('Jugador 2'), { target: { value: 'two' } })
  fireEvent.click(screen.getByRole('button', { name: 'Crear partida' }))
  await screen.findByRole('button', { name: 'Guardar resultado' })
  fireEvent.change(screen.getByLabelText('Resultado', { exact: true }), { target: { value: 'Draw' } })
  fireEvent.click(screen.getByRole('button', { name: 'Guardar resultado' }))
  expect(await screen.findByText('Versión 1 ·')).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Corregir resultado' })).toBeDisabled()
  fireEvent.change(screen.getByLabelText('Motivo de la corrección o nota'), { target: { value: 'Corrección confirmada' } })
  fireEvent.click(screen.getByRole('button', { name: 'Corregir resultado' }))
  await screen.findByText('Versión 2 · Corrección confirmada')
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/matches/match/result', expect.objectContaining({
    body: JSON.stringify({ outcome: 'Draw', kind: 'Played', reason: 'Corrección confirmada', expectedVersion: 1 }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  }))
})

it('conserva la corrección ante conflicto sin mostrarla como guardada', async () => {
  const result = { number: 1, outcome: 'Draw', kind: 'Played', reason: 'Inicial', actorPlayerId: 'one', recordedAt: '', playerOnePoints: 1, playerTwoPoints: 1 }
  const current = { ...match, version: 1, result, history: [result] }
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url.endsWith('/participants')) return response(players)
    if (url.endsWith('/result')) return response({ message: 'El resultado cambió. Recargá.' }, 409)
    return response(url.endsWith('/match') ? current : [current])
  }))
  render(<TournamentMatches tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar partidas' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Ronda 1: Ana vs Bruno' }))
  await screen.findByRole('button', { name: 'Corregir resultado' })
  fireEvent.change(screen.getByLabelText('Motivo de la corrección o nota'), { target: { value: 'Pendiente' } })
  fireEvent.click(screen.getByRole('button', { name: 'Corregir resultado' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('El resultado cambió')
  expect(screen.getByLabelText('Motivo de la corrección o nota')).toHaveValue('Pendiente')
  expect(screen.queryByText(/Versión 2/)).not.toBeInTheDocument()
})

it.each([401, 403])('cierra el acceso privado con HTTP %s', async status => {
  const lost = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => response({}, status)))
  render(<TournamentMatches tournamentId="cup" onAccessLost={lost} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar partidas' }))
  await waitFor(() => expect(lost).toHaveBeenCalledOnce())
})
