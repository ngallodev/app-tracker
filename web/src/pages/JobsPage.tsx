import { FormEvent, useEffect, useState } from "react";
import { api } from "../lib/api";
import type { Job } from "../types";

export function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [company, setCompany] = useState("");
  const [descriptionText, setDescriptionText] = useState("");
  const [sourceUrl, setSourceUrl] = useState("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      setJobs(await api.listJobs());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load jobs");
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
      await api.createJob({
        title,
        company,
        descriptionText: descriptionText || undefined,
        sourceUrl: sourceUrl || undefined
      });
      setTitle("");
      setCompany("");
      setDescriptionText("");
      setSourceUrl("");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create job");
    }
  };

  return (
    <section className="page">
      <h2>Jobs</h2>
      <form className="panel" onSubmit={onSubmit}>
        <label>
          Title
          <input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </label>
        <label>
          Company
          <input value={company} onChange={(e) => setCompany(e.target.value)} required />
        </label>
        <label>
          Description
          <textarea value={descriptionText} onChange={(e) => setDescriptionText(e.target.value)} rows={5} />
        </label>
        <label>
          Source URL
          <input value={sourceUrl} onChange={(e) => setSourceUrl(e.target.value)} />
        </label>
        <button type="submit">Create Job</button>
      </form>

      <div className="panel">
        <div className="panel-head">
          <h3>Job List</h3>
          <button type="button" onClick={() => void load()} disabled={loading}>
            Refresh
          </button>
        </div>
        {error ? <p className="error">{error}</p> : null}
        {loading ? <p>Loading...</p> : null}
        <ul className="list">
          {jobs.map((job) => (
            <li key={job.id}>
              <strong>{job.title}</strong>
              <p>{job.company}</p>
              <small>{new Date(job.createdAt).toLocaleString()}</small>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
