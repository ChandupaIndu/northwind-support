import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ticketListItem } from '../../../test/fixtures';
import type { TicketDetail } from '../../../types/api';
import { TicketEscalationPanel } from './TicketEscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getEscalations: vi.fn(),
    escalate: vi.fn(),
  },
}));

const escalationApi = vi.mocked(ticketsApi.escalate);
const historyApi = vi.mocked(ticketsApi.getEscalations);

function ticket(overrides: Partial<TicketDetail> = {}): TicketDetail {
  const listItem = ticketListItem();
  return {
    ...listItem,
    description: 'A detailed description of the support issue.',
    customer: { ...listItem.customer, email: 'customer@example.test', phone: null },
    category: { ...listItem.category, requiresSpecialist: false },
    ...overrides,
  };
}

describe('TicketEscalationPanel', () => {
  afterEach(() => vi.clearAllMocks());

  it('blocks short reasons before calling the API', async () => {
    historyApi.mockResolvedValue([]);
    render(<TicketEscalationPanel ticket={ticket()} onUpdated={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Nope' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByText('Reason must be between 5 and 500 characters.')).toBeInTheDocument();
    expect(escalationApi).not.toHaveBeenCalled();
  });

  it('shows a conflict response and leaves the displayed ticket unchanged', async () => {
    historyApi.mockResolvedValue([]);
    escalationApi.mockRejectedValue(new ApiError(409, 'Critical tickets cannot be escalated.'));
    const onUpdated = vi.fn();
    const currentTicket = ticket();
    render(<TicketEscalationPanel ticket={currentTicket} onUpdated={onUpdated} />);

    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Customer impact increased.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Critical tickets cannot be escalated.');
    await waitFor(() => expect(onUpdated).not.toHaveBeenCalled());
    expect(currentTicket.priority).toBe('High');
  });

  it('updates the ticket and refreshes escalation history after a successful escalation', async () => {
    const currentTicket = ticket();
    const escalatedTicket = ticket({ priority: 'Critical', dueAtUtc: '2026-03-02T13:00:00Z' });
    historyApi
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        {
          id: 12,
          ticketId: currentTicket.id,
          fromPriority: 'High',
          toPriority: 'Critical',
          fromAgentId: 3,
          fromAgentName: 'Ada Lovelace',
          toAgentId: 3,
          toAgentName: 'Ada Lovelace',
          fromDueAtUtc: '2026-03-02T17:00:00Z',
          toDueAtUtc: '2026-03-02T13:00:00Z',
          reason: 'Customer impact increased.',
          escalatedBy: 'Team lead',
          escalatedAtUtc: '2026-03-02T09:00:00Z',
        },
      ]);
    escalationApi.mockResolvedValue({
      ticket: escalatedTicket,
      escalation: {
        id: 12,
        ticketId: currentTicket.id,
        fromPriority: 'High',
        toPriority: 'Critical',
        fromAgentId: 3,
        fromAgentName: 'Ada Lovelace',
        toAgentId: 3,
        toAgentName: 'Ada Lovelace',
        fromDueAtUtc: '2026-03-02T17:00:00Z',
        toDueAtUtc: '2026-03-02T13:00:00Z',
        reason: 'Customer impact increased.',
        escalatedBy: 'Team lead',
        escalatedAtUtc: '2026-03-02T09:00:00Z',
      },
    });
    const onUpdated = vi.fn();
    render(<TicketEscalationPanel ticket={currentTicket} onUpdated={onUpdated} />);

    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Customer impact increased.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));

    await waitFor(() => expect(onUpdated).toHaveBeenCalledWith(escalatedTicket));
    expect(historyApi).toHaveBeenCalledTimes(2);
    expect(await screen.findByText('Customer impact increased.')).toBeInTheDocument();
    expect(screen.getByText(/High.*Critical/)).toBeInTheDocument();
  });

  it('shows an error when escalation history cannot be loaded', async () => {
    historyApi.mockRejectedValue(new ApiError(503, 'History service unavailable.'));
    render(<TicketEscalationPanel ticket={ticket()} onUpdated={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('History service unavailable.');
    expect(screen.queryByText('No escalations recorded.')).not.toBeInTheDocument();
  });
});
