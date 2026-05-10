import { useEffect, useState } from 'react';
import { adminApi, type AnalyticsEvent, type AnalyticsSummary, type AuditEntry } from '../lib/api';
import { loadAdminAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value));
}

export function AnalyticsPage() {
  const auth = loadAdminAuth();
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [events, setEvents] = useState<AnalyticsEvent[]>([]);
  const [audit, setAudit] = useState<AuditEntry[]>([]);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!auth || auth.user.role !== 'Admin') {
      return;
    }

    let cancelled = false;

    async function load() {
      try {
        const [summaryPayload, eventsPayload, auditPayload] = await Promise.all([
          adminApi.getSummary(),
          adminApi.getEvents(),
          adminApi.getAudit(),
        ]);
        if (!cancelled) {
          setSummary(summaryPayload);
          setEvents(eventsPayload.items);
          setAudit(auditPayload.items);
        }
      } catch (err) {
        if (!cancelled) {
          setMessage(err instanceof Error ? err.message : 'Unable to load analytics.');
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  if (!auth || auth.user.role !== 'Admin') {
    return <section className="page-stack"><p className="status-banner error">Sign in as an admin from the dashboard to access analytics.</p></section>;
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Analytics</p>
        <h2>Usage and operational metrics</h2>
        <p className="section-copy">Operational metrics, recent platform events, and audit history.</p>
      </header>

      {message ? <p className="status-banner">{message}</p> : null}

      {summary ? (
        <div className="card-list metric-grid">
          <article className="info-card metric-card"><span className="info-label">Active users</span><strong className="metric-value">{summary.totalActiveUsers}</strong></article>
          <article className="info-card metric-card"><span className="info-label">Active listings</span><strong className="metric-value">{summary.activeListings}</strong></article>
          <article className="info-card metric-card"><span className="info-label">Open reports</span><strong className="metric-value">{summary.openReports}</strong></article>
          <article className="info-card metric-card"><span className="info-label">Published bulletins</span><strong className="metric-value">{summary.publishedBulletins}</strong></article>
        </div>
      ) : null}

      <div className="dashboard-grid">
        <article className="info-card">
          <h3>Recent events</h3>
          <div className="stack-list">
            {events.map((event) => (
              <article key={event.id} className="stack-item">
                <strong>{event.eventName}</strong>
                <p>{event.subjectType}{event.subjectId ? ` · ${event.subjectId}` : ''}</p>
                <span className="card-meta">{formatDate(event.occurredAt)}</span>
              </article>
            ))}
            {events.length === 0 ? <p>No events yet.</p> : null}
          </div>
        </article>

        <article className="info-card">
          <h3>Audit trail</h3>
          <div className="stack-list">
            {audit.map((entry) => (
              <article key={entry.id} className="stack-item">
                <strong>{entry.action}</strong>
                <p>{entry.targetType} · {entry.targetId}</p>
                <span className="card-meta">{formatDate(entry.createdAt)} · {entry.adminEmail}</span>
              </article>
            ))}
            {audit.length === 0 ? <p>No audit entries yet.</p> : null}
          </div>
        </article>
      </div>
    </section>
  );
}
