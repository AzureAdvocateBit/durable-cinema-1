module TheaterFunctions

open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open System
open Theater

type Random with
    member this.GetValues minValue maxValue =
        Seq.initInfinite (fun _ -> this.Next(minValue, maxValue))

[<FunctionName("GetTheatersForMovie")>]
let getTheaters ([<HttpTrigger(AuthorizationLevel.Function, "get",
                               Route = "theaters/{movieId}")>] req : HttpRequest)
    (movieId : int)
    ([<DurableClient>] client : IDurableEntityClient) =
    async {
        let id = EntityId("TheatersForMovie", movieId.ToString())
        let! res = client.ReadEntityStateAsync<TheatersForMovie> id
                   |> Async.AwaitTask
        match res.EntityExists with
        | true -> return OkObjectResult res.EntityState
        | false ->
            let! theaters = getTheaters
            let r = (Random()).GetValues 1 theaters.Length |> Seq.take 3
            let t = theaters |> Array.filter (fun t -> r |> Seq.contains t.Id)
            do! client.SignalEntityAsync<ITheatersForMovie>
                    (movieId.ToString(), fun (op : ITheatersForMovie) -> op.Init t)
                |> Async.AwaitTask
            let! res = client.ReadEntityStateAsync<TheatersForMovie> id
                       |> Async.AwaitTask
            return OkObjectResult res.EntityState
    }
    |> Async.StartAsTask
