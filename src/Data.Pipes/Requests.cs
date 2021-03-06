using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Pipes
{
#region Requests

    /// <summary>
    /// An asynchronous <see cref="IRequest{TId, TData}"/> which will return another type of
    /// request at some point.
    /// </summary>
    /// <remarks>
    /// When this type of <see cref="IRequest{TId, TData}"/> is returned from a <see cref="IStage{TId, TData}"/>,
    /// it indicates to the pipeline that a series of <see cref="IRequest{TId, TData}"/> will be
    /// returned at some point in the future, and that the pipeline should wait on the contained
    /// <see cref="Task{TResult}"/> for the requests. This type of <see cref="IRequest{TId, TData}"/>
    /// will never be passed into a <see cref="IStage{TId, TData}"/>.
    /// </remarks>
    public class Async<TId, TData> : IRequest<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <summary>
        /// A <see cref="Task{TResult}"/> returning the actual requests at some point in the future.
        /// </summary>
        public Task<IEnumerable<IRequest<TId, TData>>> Requests { get; }

        /// <summary>
        /// Constructs a <see cref="Async{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        /// <param name="requests">The <see cref="Task{TResult}"/> returning the actual requests.</param>
        public Async(RequestMetadata metadata, Task<IEnumerable<IRequest<TId, TData>>> requests)
        {
            Metadata = metadata;
            Requests = requests;
        }
    }

    /// <summary>
    /// A <see cref="IRequest{TId, TData}"/> containing a dictionary of ids and their corresponding
    /// data objects.
    /// </summary>
    /// <remarks>
    /// When this type of <see cref="IRequest{TId, TData}"/> is returned from a <see cref="IStage{TId, TData}"/>,
    /// the pipeline will add it to the collection of results for a request and pass it to the
    /// previous stage to be cached. When a stage receives this type of <see cref="IRequest{TId, TData}"/>,
    /// it should attempt to cache the contained results, or return it as-is to be passed further
    /// along the pipeline.
    /// </remarks>
    public class DataSet<TId, TData> : IRequest<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <summary>
        /// The dictionary of ids and their corresponding data objects.
        /// </summary>
        public IReadOnlyDictionary<TId, TData> Results { get; }

        /// <summary>
        /// Constructs a <see cref="DataSet{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        /// <param name="results">The results contained in this <see cref="DataSet{TId, TData}"/>.</param>
        public DataSet(RequestMetadata metadata, IReadOnlyDictionary<TId, TData> results)
        {
            Metadata = metadata;
            Results = results;
        }
    }

#endregion

#region Signals

    /// <summary>
    /// A signal sent when a call to <see cref="IPipeline{TId, TData}.GetAsync(IReadOnlyCollection{TId}, CancellationToken)"/>
    /// is about to complete.
    /// </summary>
    public class PipelineComplete<TId, TData> : ISignal<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <summary>
        /// Constructs a <see cref="PipelineComplete{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        internal PipelineComplete(RequestMetadata metadata)
        {
            Metadata = metadata;
        }
    }

    /// <summary>
    /// A signal sent when data has been read from the <see cref="ISource{TId, TData}"/> in a
    /// <see cref="IPipeline{TId, TData}"/>.
    /// </summary>
    public class SourceRead<TId, TData> : ISignal<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <summary>
        /// Constructs a <see cref="SourceRead{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        internal SourceRead(RequestMetadata metadata)
        {
            Metadata = metadata;
        }
    }

#endregion

#region Queries

    /// <summary>
    /// A query to retrieve a given series of ids.
    /// </summary>
    /// <remarks>
    /// When this type of <see cref="IRequest{TId, TData}"/> is returned from a <see cref="IStage{TId, TData}"/>,
    /// the pipeline will pass it to the next stage. When a stage receives this type of <see cref="IRequest{TId, TData}"/>,
    /// it should attempt to retrieve as many of the contained ids as possible, and any remaining
    /// ids should be returned in another <see cref="Query{TId, TData}"/>.
    /// </remarks>
    public class Query<TId, TData> : IQuery<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <inheritdoc/>
        public IReadOnlyCollection<TId> Ids { get; }

        /// <summary>
        /// Constructs a <see cref="Query{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        /// <param name="ids">The series of ids to be retrieved.</param>
        public Query(RequestMetadata metadata, IReadOnlyCollection<TId> ids)
        {
            Metadata = metadata;
            Ids = ids;
        }
    }

    /// <summary>
    /// A request to try and retrieve the given series of ids again.
    /// </summary>
    /// <remarks>
    /// This type of <see cref="IRequest{TId, TData}"/> is similar to a <see cref="Query{TId, TData}"/>,
    /// however instead of the pipeline passing it to the next stage, it will pass it to the
    /// previous stage. Stages should treat this the the same as a <see cref="Query{TId, TData}"/>,
    /// however, if only partial results can be returned, a <see cref="Retry{TId, TData}"/> with
    /// the remaining ids should not be returned.
    /// </remarks>
    public class Retry<TId, TData> : IQuery<TId, TData>
    {
        /// <inheritdoc/>
        public RequestMetadata Metadata { get; }

        /// <inheritdoc/>
        public IReadOnlyCollection<TId> Ids { get; }

        /// <summary>
        /// Constructs a <see cref="Retry{TId, TData}"/>.
        /// </summary>
        /// <param name="metadata">Metadata about the request and the pipeline it's a part of.</param>
        /// <param name="ids">The series of ids to be retrieved.</param>
        public Retry(RequestMetadata metadata, IReadOnlyCollection<TId> ids)
        {
            Metadata = metadata;
            Ids = ids;
        }
    }

#endregion
}
