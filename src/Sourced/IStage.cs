using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sourced
{
    /// <summary>
    /// A stage in a <see cref="IPipeline{TId, TData}"/> which can be used to alter queries passing
    /// through, cache data, or perform other actions.
    /// </summary>
    /// <typeparam name="TId">
    /// The type of the id objects used in the <see cref="IPipeline{TId, TData}"/> this stage is a
    /// part of.
    /// </typeparam>
    /// <typeparam name="TData">
    /// The type of the data objects used in the <see cref="IPipeline{TId, TData}"/> this stage is a
    /// part of.
    /// </typeparam>
    public interface IStage<TId, TData>
    {
        /// <summary>
        /// Processes a <see cref="IRequest{TId, TData}"/>.
        /// </summary>
        /// <param name="request">The request to process.</param>
        /// <param name="token">
        /// A cancellation token used to cancel the request. Partial results may be returned if
        /// available.
        /// </param>
        /// <returns>A series of further requests to be processed by the pipeline.</returns>
        IEnumerable<IRequest<TId, TData>> Process(IRequest<TId, TData> request, CancellationToken token);
    }
}
