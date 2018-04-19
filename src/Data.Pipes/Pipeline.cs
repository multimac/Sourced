using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Data.Pipes.StateMachine;

namespace Data.Pipes
{
    public class Pipeline<TId, TData> : IPipeline<TId, TData>
    {
        private readonly ISource<TId, TData> _source;
        private readonly IStage<TId, TData>[] _stages;

        public PipelineMetadata Metadata => PipelineMetadata
            .CreateFromSource<TId, TData>(_source);

        public Pipeline(ISource<TId, TData> source, IEnumerable<IStage<TId, TData>> stages)
            : this(source, stages.ToArray()) { }
        public Pipeline(ISource<TId, TData> source, params IStage<TId, TData>[] stages)
        {
            _source = source;
            _stages = stages;
        }

        public async Task<IReadOnlyDictionary<TId, TData>> GetAsync(IReadOnlyCollection<TId> ids, CancellationToken token = default)
        {
            var machine = new CoreStateMachine<TId, TData>();
            var state = new State<TId, TData>(machine, token);
            var query = new Query<TId, TData>(this, ids);

            try
            {
                await Task.Run(() => RequestStageAsync(state, query));
            }
            catch (AggregateException ex)
            {
                throw new PipelineException<TId, TData>(state.GetResults(), ex.Flatten().InnerExceptions);
            }
            catch (Exception ex)
            {
                throw new PipelineException<TId, TData>(state.GetResults(), ex);
            }

            return state.GetResults();
        }

        private async Task ProcessRequestBatchAsync(State<TId, TData> state, IEnumerable<IRequest<TId, TData>> requests)
        {
            var collected = new List<IRequest<TId, TData>>();
            var exceptions = new List<Exception>();

            try
            {
                foreach (var request in requests)
                    collected.Add(request);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            var processed = collected.Select(request => ProcessRequestAsync(state, request));

            try
            {
                await Task.WhenAll(processed);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        private async Task ProcessRequestAsync(State<TId, TData> state, IRequest<TId, TData> request)
        {
            if (request is Async<TId, TData> asyncRequest)
            {
                IEnumerable<IRequest<TId, TData>> requests;

                try
                {
                    requests = await asyncRequest.Requests;
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == state.Token)
                {
                    return;
                }

                await ProcessRequestBatchAsync(state, requests);
            }

            await RequestStageAsync(state.Handle(request), request);
        }

        private Task RequestStageAsync(State<TId, TData> state, IRequest<TId, TData> request)
        {
            if (state.Index < -1 || state.Index > _stages.Length)
            {
                throw new IndexOutOfRangeException("Pipeline state has transitioned out-of-bounds.");
            }
            else if (state.Index == _stages.Length)
            {
                return QuerySourceAsync(state, request as Query<TId, TData>);
            }
            else if (state.Index == -1)
            {
                return Task.CompletedTask;
            }

            var stage = _stages[state.Index];
            var requests = stage.Process(request, state.Token);

            return ProcessRequestBatchAsync(state, requests);
        }
        private async Task QuerySourceAsync(State<TId, TData> state, Query<TId, TData> query)
        {
            IReadOnlyDictionary<TId, TData> results;

            try
            {
                results = await _source.ReadAsync(query, state.Token);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == state.Token)
            {
                return;
            }

            var data = new DataSet<TId, TData>(this, results);
            await RequestStageAsync(state.Handle(data), data);
        }
    }
}