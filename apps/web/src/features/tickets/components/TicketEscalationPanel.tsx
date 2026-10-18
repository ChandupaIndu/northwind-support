import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface TicketEscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

/** Escalation form and its immutable audit history. */
export function TicketEscalationPanel({ ticket, onUpdated }: TicketEscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [historyError, setHistoryError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();
  const [validationError, setValidationError] = useState<string>();

  const loadHistory = useCallback(() => ticketsApi.getEscalations(ticket.id), [ticket.id]);

  const reloadHistory = async () => {
    setIsLoadingHistory(true);
    setHistoryError(undefined);
    try {
      setHistory(await loadHistory());
    } catch (error) {
      setHistoryError(toErrorMessage(error, 'Could not load escalation history.'));
    } finally {
      setIsLoadingHistory(false);
    }
  };

  useEffect(() => {
    let isCurrent = true;
    void loadHistory()
      .then((result) => {
        if (isCurrent) setHistory(result);
      })
      .catch((error: unknown) => {
        if (isCurrent) setHistoryError(toErrorMessage(error, 'Could not load escalation history.'));
      })
      .finally(() => {
        if (isCurrent) setIsLoadingHistory(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [loadHistory]);

  const unavailable = ticket.priority === 'Critical' || ticket.status === 'Resolved' || ticket.status === 'Closed';

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const cleanReason = reason.trim();
    if (cleanReason.length < 5 || cleanReason.length > 500) {
      setValidationError('Reason must be between 5 and 500 characters.');
      return;
    }

    setValidationError(undefined);
    setSubmitError(undefined);
    setIsSaving(true);
    try {
      const result = await ticketsApi.escalate(ticket.id, cleanReason);
      onUpdated(result.ticket);
      setReason('');
      await reloadHistory();
    } catch (error) {
      setSubmitError(toErrorMessage(error, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="card" aria-labelledby="escalation-heading">
      <h2 id="escalation-heading">Escalation</h2>
      {unavailable ? (
        <p>This ticket cannot be escalated in its current state.</p>
      ) : (
        <form className="form" onSubmit={(event) => void submit(event)} noValidate>
          <div className="field">
            <label className="field__label" htmlFor="escalation-reason">Reason</label>
            <textarea
              id="escalation-reason"
              value={reason}
              maxLength={500}
              rows={3}
              onChange={(event) => setReason(event.target.value)}
              aria-describedby={validationError ? 'escalation-error' : undefined}
            />
          </div>
          {validationError && <p id="escalation-error" className="field__error" role="alert">{validationError}</p>}
          {submitError && <p className="field__error" role="alert">{submitError}</p>}
          <button type="submit" className="button button--primary" disabled={isSaving}>
            {isSaving ? 'Escalating...' : 'Escalate ticket'}
          </button>
        </form>
      )}

      <h3>History</h3>
      {historyError && <p role="alert">{historyError}</p>}
      {isLoadingHistory && <p>Loading escalation history...</p>}
      {!isLoadingHistory && !historyError && history.length === 0 && <p>No escalations recorded.</p>}
      {history.length > 0 && (
        <ol className="escalation-history">
          {history.map((entry) => (
            <li key={entry.id}>
              <p><strong>{entry.fromPriority} → {entry.toPriority}</strong> by {entry.escalatedBy} at {formatDateTime(entry.escalatedAtUtc)}</p>
              <p>Assignee: {entry.fromAgentName ?? 'Unassigned'} → {entry.toAgentName ?? 'Unassigned'}</p>
              <p>Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}</p>
              <p>{entry.reason}</p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
