import { useEffect, useState } from 'react';
import { adminApi, type AdminListingReport, type AdminListingSummary } from '../lib/api';
import { loadAdminAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value));
}

export function ListingsModerationPage() {
  const auth = loadAdminAuth();
  const [listings, setListings] = useState<AdminListingSummary[]>([]);
  const [reports, setReports] = useState<AdminListingReport[]>([]);
  const [selectedListing, setSelectedListing] = useState<AdminListingSummary | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function loadListings() {
    try {
      const payload = await adminApi.getListings('?page=1&pageSize=20');
      setListings(payload.items);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load listings.');
    }
  }

  useEffect(() => {
    if (!auth || auth.user.role !== 'Admin') {
      return;
    }
    void loadListings();
  }, []);

  async function inspectListing(listing: AdminListingSummary) {
    try {
      setSelectedListing(listing);
      const payload = await adminApi.getListingReports(listing.id);
      setReports(payload);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load listing reports.');
    }
  }

  async function moderate(action: 'remove' | 'restore', listingId: string) {
    const reason = window.prompt(`Reason for ${action}ing this listing:`)?.trim();
    if (!reason) {
      return;
    }

    try {
      if (action === 'remove') {
        await adminApi.removeListing(listingId, reason);
      } else {
        await adminApi.restoreListing(listingId, reason);
      }
      await loadListings();
      setMessage(`Listing ${action}d.`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : `Unable to ${action} listing.`);
    }
  }

  if (!auth || auth.user.role !== 'Admin') {
    return <section className="page-stack"><p className="status-banner error">Sign in as an admin from the dashboard to access listing moderation.</p></section>;
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Listings</p>
        <h2>Listing moderation</h2>
        <p className="section-copy">Review ownership, report counts, and moderation status transitions.</p>
      </header>

      {message ? <p className="status-banner">{message}</p> : null}

      <div className="dashboard-grid">
        <article className="info-card">
          <div className="table-stack">
            {listings.map((listing) => (
              <button key={listing.id} className="table-row button-reset" type="button" onClick={() => void inspectListing(listing)}>
                <div>
                  <strong>{listing.title}</strong>
                  <p>{listing.ownerEmail}</p>
                </div>
                <div className="row-meta">
                  <span className="chip subdued">{listing.status}</span>
                  <span className="card-meta">{listing.reportCount} reports</span>
                </div>
              </button>
            ))}
          </div>
        </article>

        <article className="info-card">
          {selectedListing ? (
            <div className="page-stack">
              <div>
                <h3>{selectedListing.title}</h3>
                <p>{selectedListing.category} · posted {formatDate(selectedListing.createdAt)}</p>
              </div>
              <div className="button-row">
                <button className="secondary-button danger" type="button" onClick={() => void moderate('remove', selectedListing.id)}>Remove</button>
                <button className="secondary-button" type="button" onClick={() => void moderate('restore', selectedListing.id)}>Restore</button>
              </div>
              <div className="stack-list">
                {reports.length > 0 ? reports.map((report) => (
                  <article key={report.id} className="stack-item">
                    <strong>{report.reasonCode}</strong>
                    <p>{report.description || 'No additional details provided.'}</p>
                    <span className="card-meta">{report.reporterEmail} · {formatDate(report.createdAt)}</span>
                  </article>
                )) : <p>No reports found for this listing.</p>}
              </div>
            </div>
          ) : (
            <p>Select a listing to review its report history.</p>
          )}
        </article>
      </div>
    </section>
  );
}
