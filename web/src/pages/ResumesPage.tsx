import { FormEvent, useEffect, useState } from "react";
import { api } from "../lib/api";
import type { Resume } from "../types";

export function ResumesPage() {
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [content, setContent] = useState("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      setResumes(await api.listResumes());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load resumes");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    try {
      await api.createResume({ name, content });
      setName("");
      setContent("");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create resume");
    }
  };

  return (
    <section className="page">
      <h2>Resumes</h2>
      <form className="panel" onSubmit={onSubmit}>
        <label>
          Name
          <input value={name} onChange={(e) => setName(e.target.value)} required />
        </label>
        <label>
          Resume Content
          <textarea value={content} onChange={(e) => setContent(e.target.value)} rows={8} required />
        </label>
        <button type="submit">Create Resume</button>
      </form>

      <div className="panel">
        <div className="panel-head">
          <h3>Resume List</h3>
          <button type="button" onClick={() => void load()} disabled={loading}>
            Refresh
          </button>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {loading ? <p>Loading...</p> : null}
        <ul className="list">
          {resumes.map((resume) => (
            <li key={resume.id}>
              <strong>{resume.name}</strong>
              <p>{resume.contentPreview ?? "No preview"}</p>
              <small>{new Date(resume.createdAt).toLocaleString()}</small>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
