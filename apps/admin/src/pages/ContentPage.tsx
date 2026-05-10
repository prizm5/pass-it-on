import { useEffect, useState } from 'react';
import { adminApi, type AdminBulletin, type AdminContentPage } from '../lib/api';
import { loadAdminAuth } from '../lib/auth';

const contentTypes = ['Faq', 'Policy'] as const;

export function ContentPage() {
  const auth = loadAdminAuth();
  const [bulletins, setBulletins] = useState<AdminBulletin[]>([]);
  const [pages, setPages] = useState<AdminContentPage[]>([]);
  const [message, setMessage] = useState<string | null>(null);

  const [bulletinTitle, setBulletinTitle] = useState('');
  const [bulletinBody, setBulletinBody] = useState('');
  const [pageSlug, setPageSlug] = useState('faq');
  const [pageTitle, setPageTitle] = useState('Frequently Asked Questions');
  const [pageBody, setPageBody] = useState('');
  const [contentType, setContentType] = useState<(typeof contentTypes)[number]>('Faq');

  async function loadContent() {
    try {
      const [bulletinsPayload, pagesPayload] = await Promise.all([
        adminApi.getBulletins(),
        adminApi.getPages(),
      ]);
      setBulletins(bulletinsPayload.items);
      setPages(pagesPayload);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load content.');
    }
  }

  useEffect(() => {
    if (!auth || auth.user.role !== 'Admin') {
      return;
    }
    void loadContent();
  }, []);

  async function createBulletin(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await adminApi.createBulletin({ title: bulletinTitle, body: bulletinBody });
      setBulletinTitle('');
      setBulletinBody('');
      await loadContent();
      setMessage('Bulletin created.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to create bulletin.');
    }
  }

  async function toggleBulletinPublish(bulletin: AdminBulletin) {
    try {
      if (bulletin.status === 'Published') {
        await adminApi.unpublishBulletin(bulletin.id);
      } else {
        await adminApi.publishBulletin(bulletin.id);
      }
      await loadContent();
      setMessage('Bulletin updated.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to update bulletin.');
    }
  }

  async function createPage(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      await adminApi.createPage({ slug: pageSlug, title: pageTitle, body: pageBody, contentType });
      setPageBody('');
      await loadContent();
      setMessage('Page created.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to create page.');
    }
  }

  async function publishPage(pageId: string) {
    try {
      await adminApi.publishPage(pageId);
      await loadContent();
      setMessage('Page published.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to publish page.');
    }
  }

  if (!auth || auth.user.role !== 'Admin') {
    return <section className="page-stack"><p className="status-banner error">Sign in as an admin from the dashboard to manage content.</p></section>;
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Content</p>
        <h2>Bulletins, FAQ, and policy management</h2>
        <p className="section-copy">Create bulletin posts and publish static content pages used by the public app.</p>
      </header>

      {message ? <p className="status-banner">{message}</p> : null}

      <div className="dashboard-grid">
        <form className="form-card" onSubmit={createBulletin}>
          <h3>New bulletin</h3>
          <label>
            <span>Title</span>
            <input value={bulletinTitle} onChange={(event) => setBulletinTitle(event.target.value)} required />
          </label>
          <label>
            <span>Body</span>
            <textarea rows={5} value={bulletinBody} onChange={(event) => setBulletinBody(event.target.value)} required />
          </label>
          <button className="primary-button" type="submit">Create bulletin</button>
        </form>

        <form className="form-card" onSubmit={createPage}>
          <h3>New content page</h3>
          <label>
            <span>Slug</span>
            <input value={pageSlug} onChange={(event) => setPageSlug(event.target.value)} required />
          </label>
          <label>
            <span>Title</span>
            <input value={pageTitle} onChange={(event) => setPageTitle(event.target.value)} required />
          </label>
          <label>
            <span>Content type</span>
            <select value={contentType} onChange={(event) => setContentType(event.target.value as (typeof contentTypes)[number])}>
              {contentTypes.map((type) => <option key={type} value={type}>{type}</option>)}
            </select>
          </label>
          <label>
            <span>Body</span>
            <textarea rows={5} value={pageBody} onChange={(event) => setPageBody(event.target.value)} required />
          </label>
          <button className="primary-button" type="submit">Create page</button>
        </form>
      </div>

      <div className="dashboard-grid">
        <article className="info-card">
          <h3>Bulletins</h3>
          <div className="stack-list">
            {bulletins.map((bulletin) => (
              <article key={bulletin.id} className="stack-item">
                <strong>{bulletin.title}</strong>
                <p>{bulletin.body}</p>
                <div className="button-row">
                  <span className="chip subdued">{bulletin.status}</span>
                  <button className="secondary-button" type="button" onClick={() => void toggleBulletinPublish(bulletin)}>
                    {bulletin.status === 'Published' ? 'Unpublish' : 'Publish'}
                  </button>
                </div>
              </article>
            ))}
            {bulletins.length === 0 ? <p>No bulletins yet.</p> : null}
          </div>
        </article>

        <article className="info-card">
          <h3>Content pages</h3>
          <div className="stack-list">
            {pages.map((page) => (
              <article key={page.id} className="stack-item">
                <strong>{page.title}</strong>
                <p>{page.slug} · {page.contentType}</p>
                <div className="button-row">
                  <span className="chip subdued">{page.status}</span>
                  {page.status !== 'Published' ? (
                    <button className="secondary-button" type="button" onClick={() => void publishPage(page.id)}>Publish</button>
                  ) : null}
                </div>
              </article>
            ))}
            {pages.length === 0 ? <p>No content pages yet.</p> : null}
          </div>
        </article>
      </div>
    </section>
  );
}
