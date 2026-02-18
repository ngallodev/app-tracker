import { useState, useEffect } from 'react'

interface Job {
  id: string;
  title: string;
  company: string;
  descriptionText: string;
}

interface Resume {
  id: string;
  name: string;
  content: string;
}

function App() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [jobsRes, resumesRes] = await Promise.all([
        fetch('/api/jobs'),
        fetch('/api/resumes')
      ]);
      
      if (!jobsRes.ok || !resumesRes.ok) {
        throw new Error('Failed to fetch data');
      }
      
      setJobs(await jobsRes.json());
      setResumes(await resumesRes.json());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div style={{ padding: '2rem' }}>Loading...</div>;
  if (error) return <div style={{ padding: '2rem', color: 'red' }}>Error: {error}</div>;

  return (
    <div style={{ padding: '2rem', maxWidth: '1200px', margin: '0 auto' }}>
      <h1>AI Job Application Tracker</h1>
      
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2rem', marginTop: '2rem' }}>
        <div>
          <h2>Jobs ({jobs.length})</h2>
          <ul>
            {jobs.map(job => (
              <li key={job.id}>
                <strong>{job.title}</strong> at {job.company}
              </li>
            ))}
          </ul>
        </div>
        
        <div>
          <h2>Resumes ({resumes.length})</h2>
          <ul>
            {resumes.map(resume => (
              <li key={resume.id}>{resume.name}</li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

export default App