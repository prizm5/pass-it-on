import { useEffect, useState } from 'react';
import { adminApi, type AdminReportDetail } from '../lib/api';
import { loadAdminAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value));
}

export function ReportsPage() {
  const auth = loadAdminAuth();
  const [reports, setReports] = useState<AdminReportDetail[]>([]);
  const [status, setStatus] = useState('Open');
  const [message, setMessage] = useState<string | null>(null);

  async function loadReports(currentStatus = status) {
    try {
      const query = `?status=${encodeURIComponent(currentStatus)}&page=1&pageSize=20`;
      const payload = await adminApi.getReports(query);
      setReports(payload.items);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load reports.');
    }
  }

  useEffect(() => {
    if (!auth || auth.user.role !== 'Admin') {
      return;
    }
    void loadReports('Open');
  }, []);

  async function resolve(reportId: string, removeListing: boolean) {
    const reason = window.prompt('Reason for resolving this report:')?.trim() ?? '';
    try {
      await adminApi.resolveReport(reportId, reason, removeListing);
      await loadReports();
      setMessage('Report resolved.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to resolve report.');
    }
  }

  async function dismiss(reportId: string) {
    const reason = window.prompt('Reason for dismissing this report:')?.trim() ?? '';
    try {
      await adminApi.dismissReport(reportId, reason);
      await loadReports();
      setMessage('Report dismissed.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to dismiss report.');
    }
  }

  if (!auth || auth.user.role !== 'Admin') {
    return <section className="page-stack"><p className="status-banner error">Sign in as an admin from the dashboard to access report review.</p></section>;
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Reports</p>
        <h2>Report review workflow</h2>
        <p className="section-copy">Resolve harmful content, optionally remove the listing, or dismiss invalid reports.</p>
      </header>

      <article className="info-card filter-card">
        <div className="filter-grid single-action-grid">
          <label>
            <span>Status</span>
            <select value={status} onChange={(event) => setStatus(event.target.value)}>
              <option value="Open">Open</option>
              <option value="Resolved">Resolved</option>
              <option value="Dismissed">Dismissed</option>
            </select>
          </label>
          <button className="primary-button" type="button" onClick={() => void loadReports(status)}>Refresh</button>
        </div>
      </article>

      {message ? <p className="status-banner">{message}</p> : null}

      <div className="stack-list">
        {reports.map((report) => (
          <article key={report.id} className="info-card">
            <div className="section-heading compact-heading">
              <div>
                <p className="eyebrow">{report.reasonCode}</p>
                <h3>{report.listingTitle}</h3>
              </div>
              <span className="chip subdued">{report.status}</span>
            </div>
            <p>{report.description || 'No additional details provided.'}</p>
            <div className="info-grid compact">
              <div><span className="info-label">Reporter</span><strong>{report.reporterEmail}</strong></div>
              <div><span className="info-label">Created</span><strong>{formatDate(report.createdAt)}</strong></div>
            </div>
            {report.status === 'Open' ? (
              <div className="button-row">
                <button className="primary-button" type="button" onClick={() => void resolve(report.id, false)}>Resolve</button>
                <button className="secondary-button danger" type="button" onClick={() => void resolve(report.id, true)}>Resolve + remove listing</button>
                <button className="ghost-button" type="button" onClick={() => void dismiss(report.id)}>Dismiss</button>
              </div>
            ) : null}
          </article>
        ))}
        {reports.length === 0 ? <article className="info-card"><p>No reports matched the selected status.</p></article> : null}
      </div>
    </section>
  );
}
