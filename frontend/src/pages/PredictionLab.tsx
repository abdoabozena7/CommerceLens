import { ArrowRight, BrainCircuit, FlaskConical, RefreshCw, ShieldCheck, Sparkles } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, Loading, PageHeading, Panel } from '../components/Shared'
import { dateTime, errorMessage, number, percent, post } from '../services/api'
import type { FilterOptions, ModelMetrics, ModelStatus, Prediction } from '../services/types'
import { useResource } from '../services/useResource'

export function PredictionLab() {
  const status = useResource<ModelStatus>('/model/status')
  const options = useResource<FilterOptions>('/analytics/options')
  const [values, setValues] = useState({ deviceType: 'Mobile', adPosition: 'Top', browsingHistory: 'Shopping', timeOfDay: 'Morning' })
  const [prediction, setPrediction] = useState<Prediction>()
  const [busy, setBusy] = useState(false)
  const [training, setTraining] = useState(false)
  const [error, setError] = useState('')
  async function predict(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setPrediction(undefined)
    try { setPrediction(await post<Prediction>('/predictions', values)) }
    catch (error) { setError(errorMessage(error)) }
    finally { setBusy(false) }
  }
  async function train() {
    setTraining(true); setError(''); setPrediction(undefined)
    try { await post<ModelMetrics>('/model/train') }
    catch (error) { setError(errorMessage(error)) }
    finally { setTraining(false) }
  }
  const fields = [
    { key: 'deviceType', label: 'Device type', options: 'deviceTypes' },
    { key: 'adPosition', label: 'Ad position', options: 'adPositions' },
    { key: 'browsingHistory', label: 'Browsing history', options: 'browsingHistories' },
    { key: 'timeOfDay', label: 'Time of day', options: 'timesOfDay' },
  ] as const
  const metrics = status.data?.metrics
  return <><PageHeading eyebrow="ML.NET / EXPERIMENT, DON'T ASSUME" title="Prediction Lab" description="Explore click likelihood with a small logistic regression model. A probability is not a promise."
    actions={<button className="button secondary" onClick={() => void train()} disabled={training}><RefreshCw size={14} className={training ? 'spin' : ''} />{training ? 'Training & evaluating...' : 'Retrain model'}</button>} />
    {error && <ErrorState message={error} />}{status.error && <ErrorState message={status.error} retry={status.refresh} />}{options.error && <ErrorState message={options.error} retry={options.refresh} />}
    {status.loading && <Loading />}
    {status.data?.isStale && <div className="notice error" role="alert">New observations were imported after this model was trained. Retrain before interpreting its estimates on the expanded dataset.</div>}
    <div className="prediction-layout"><Panel title="Build an ad context" subtitle="Four categorical features. No names, user IDs, age, or gender.">
      <form className="prediction-form" onSubmit={event => void predict(event)}>{fields.map(field => <label key={field.key}>{field.label}<select aria-label={`Predict ${field.label.toLowerCase()}`} value={values[field.key]}
        onChange={event => { setValues({ ...values, [field.key]: event.target.value }); setPrediction(undefined) }}>
        {[...new Set([...(options.data?.[field.options] ?? []), 'Unknown'])].map(value => <option key={value}>{value}</option>)}</select></label>)}
        <p className="muted">Missing categorical features are encoded as Unknown.</p>
        <button className="button primary wide" disabled={busy || training || !metrics || !options.data} type="submit"><FlaskConical size={16} />{busy ? 'Estimating...' : 'Predict click probability'}<ArrowRight size={14} /></button></form>
    </Panel>
      <Panel title="Estimated click probability" subtitle="One model estimate, compared with observed history.">
        {prediction ? <div className="prediction-result"><div className="prediction-orbit"><Sparkles size={20} /><strong data-testid="prediction-probability">{percent(prediction.probability)}</strong><span>estimated probability</span></div>
          <div className="prediction-compare"><span>Historical baseline CTR<strong>{percent(prediction.historicalBaselineCtr)}</strong></span>
            <span>Difference from baseline<strong>{((prediction.probability - prediction.historicalBaselineCtr) * 100).toFixed(1)} percentage points</strong></span></div>
          <p><span className="badge lilac">{prediction.engine} / {prediction.sourceType}</span></p>
          <p>Trained {dateTime(prediction.modelTrainedAt)}. {prediction.modelStale ? 'This model is stale; retrain it.' : 'This is not a guaranteed click or a purchase prediction.'}</p>
        </div> : <div className="prediction-empty"><span><BrainCircuit size={37} strokeWidth={1.3} /></span><h3>Give your data a question.</h3><p>Select an ad context to estimate its click probability and compare it with the historical baseline.</p></div>}
      </Panel></div>
    {metrics && <><Panel title="Measured model evaluation" subtitle={`${metrics.algorithm} / evaluated ${dateTime(metrics.trainedAt)} / ${metrics.trainingSeconds.toFixed(2)} seconds of training and evaluation`}>
      <div className="evaluation-grid">{[['Accuracy', metrics.accuracy], ['Precision', metrics.precision], ['Recall', metrics.recall], ['ROC-AUC', metrics.rocAuc], ['F1 score', metrics.f1Score], ['Majority baseline accuracy', metrics.baselineAccuracy]].map(([label, value]) => <div key={String(label)}><span>{label}</span>
        <strong>{label === 'ROC-AUC' ? Number(value).toFixed(4) : percent(Number(value))}</strong></div>)}</div>
      <div className="evaluation-note"><ShieldCheck size={18} /><div><strong>User-disjoint evaluation</strong><p>{number(metrics.trainRows)} training observations from {number(metrics.trainUsers)} users. {number(metrics.testRows)} test observations from {number(metrics.testUsers)} users. <b>{metrics.userOverlap} overlapping users.</b></p>
        <p>Users are split approximately 80/20 with seed 42. Category encoders are fit on training data only. Log loss: {metrics.logLoss.toFixed(4)}.</p></div></div>
      <div className="table-scroll"><table className="data-table"><caption>Held-out confusion matrix (threshold 0.5)</caption><thead><tr><th>Actual outcome</th><th>Predicted click</th><th>Predicted no click</th></tr></thead>
        <tbody><tr><th>Click</th><td>{number(metrics.truePositives)}</td><td>{number(metrics.falseNegatives)}</td></tr>
          <tr><th>No click</th><td>{number(metrics.falsePositives)}</td><td>{number(metrics.trueNegatives)}</td></tr></tbody></table></div>
      <div className="panel-bottom"><span>Training features: {metrics.features.join(', ')}</span><span className="badge rose">MODEL OUTPUT, NOT FACT</span></div>
    </Panel>
      <div className="notice"><span className="badge sand">READ RESULTS HONESTLY</span><span>Compare accuracy with the majority-class baseline on the same held-out users. Weak ROC-AUC or no baseline improvement means the model may have little useful predictive signal. There is no promised performance.</span></div>
    </>}
  </>
}
