import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { SlaCell } from './SlaCell';

describe('SlaCell', () => {
  it.each([
    ['WithinSla', 'Within SLA', 'badge--info'],
    ['AtRisk', 'At risk', 'badge--warning'],
    ['Breached', 'Breached', 'badge--danger'],
    ['Met', 'Met', 'badge--success'],
    ['NotApplicable', 'No SLA', 'badge--neutral'],
  ] as const)('renders %s with a distinct text label', (status, label, tone) => {
    render(<SlaCell dueAtUtc={null} slaStatus={status} />);
    expect(screen.getByText(label)).toHaveClass(tone);
  });
});
