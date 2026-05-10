import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { loadAuth } from '../lib/auth';

const conditions = ['New', 'LikeNew', 'Good', 'Fair', 'Worn'] as const;
const contactPreferences = ['Email', 'Phone', 'ProfileContact', 'Other'] as const;

export function CreateListingPage() {
  const navigate = useNavigate();
  const auth = loadAuth();
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState('Outerwear');
  const [size, setSize] = useState('');
  const [ageRange, setAgeRange] = useState('');
  const [condition, setCondition] = useState<(typeof conditions)[number]>('Good');
  const [contactPreference, setContactPreference] = useState<(typeof contactPreferences)[number]>('ProfileContact');
  const [images, setImages] = useState<File[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function readImageDimensions(file: File) {
    const objectUrl = URL.createObjectURL(file);
    try {
      const dimensions = await new Promise<{ width: number; height: number }>((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve({ width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = () => reject(new Error('Invalid image file.'));
        img.src = objectUrl;
      });
      return dimensions;
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setSubmitting(true);
      setStatus('Creating draft listing...');
      const created = await api.createListing({
        title,
        description,
        category,
        size: size || null,
        ageRange: ageRange || null,
        condition,
        contactPreference,
      });

      for (let index = 0; index < images.length; index += 1) {
        const file = images[index];
        setStatus(`Uploading image ${index + 1} of ${images.length}...`);
        const upload = await api.requestListingImageUploadUrl(created.id, {
          fileName: file.name,
          contentType: file.type,
          fileSizeBytes: file.size,
        });

        const uploadResponse = await fetch(upload.uploadUrl, {
          method: 'PUT',
          headers: {
            'Content-Type': file.type,
          },
          body: file,
        });

        if (!uploadResponse.ok) {
          throw new Error(`Image upload failed (${uploadResponse.status}).`);
        }

        const dimensions = await readImageDimensions(file);
        await api.attachListingImage(created.id, {
          storageKey: upload.storageKey,
          width: dimensions.width,
          height: dimensions.height,
          sortOrder: index,
        });
      }

      setStatus('Publishing listing...');
      await api.activateListing(created.id);
      navigate(`/listings/${created.id}`);
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Unable to create listing.');
    } finally {
      setSubmitting(false);
    }
  }

  if (!auth) {
    return (
      <section className="page-stack">
        <header className="section-heading">
          <p className="eyebrow">Create listing</p>
          <h2>Sign in before posting</h2>
        </header>
        <article className="info-card">
          <p>This form uses your authenticated profile and contact preference. Sign in first.</p>
          <Link className="primary-button" to="/profile">Go to profile</Link>
        </article>
      </section>
    );
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Create listing</p>
        <h2>Post a clothing item</h2>
        <p className="section-copy">
          Add item details and optional photos. Images are uploaded securely before the listing is published.
        </p>
      </header>

      <form className="form-card" onSubmit={handleSubmit}>
        <div className="form-grid">
          <label>
            <span>Title</span>
            <input value={title} onChange={(event) => setTitle(event.target.value)} required maxLength={200} />
          </label>
          <label>
            <span>Category</span>
            <input value={category} onChange={(event) => setCategory(event.target.value)} required maxLength={100} />
          </label>
          <label>
            <span>Size</span>
            <input value={size} onChange={(event) => setSize(event.target.value)} maxLength={50} />
          </label>
          <label>
            <span>Age range</span>
            <input value={ageRange} onChange={(event) => setAgeRange(event.target.value)} maxLength={50} />
          </label>
          <label>
            <span>Condition</span>
            <select value={condition} onChange={(event) => setCondition(event.target.value as (typeof conditions)[number])}>
              {conditions.map((item) => (
                <option key={item} value={item}>{item}</option>
              ))}
            </select>
          </label>
          <label>
            <span>Contact preference</span>
            <select
              value={contactPreference}
              onChange={(event) => setContactPreference(event.target.value as (typeof contactPreferences)[number])}
            >
              {contactPreferences.map((item) => (
                <option key={item} value={item}>{item}</option>
              ))}
            </select>
          </label>
          <label className="full-width">
            <span>Description</span>
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              required
              rows={7}
              maxLength={4000}
              placeholder="Describe the item, wear level, stains, and what makes pickup easier."
            />
          </label>
          <label className="full-width">
            <span>Photos (optional, JPEG/PNG)</span>
            <input
              type="file"
              accept="image/jpeg,image/png"
              multiple
              onChange={(event) => setImages(Array.from(event.target.files ?? []))}
            />
          </label>
        </div>

        <div className="button-row">
          <button className="primary-button" type="submit" disabled={submitting}>
            {submitting ? 'Saving...' : 'Create and publish'}
          </button>
          <Link className="ghost-button" to="/listings">Back to listings</Link>
        </div>

        {status ? <p className="form-message">{status}</p> : null}
      </form>
    </section>
  );
}
