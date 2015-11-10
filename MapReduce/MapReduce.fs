// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.
open System
open System.IO

//On peut mettre n'importe quel fonction, x est un mot donc String.lenght(x) > 56 compterais les mot plus grand que 56
let EvalFun x = true

(*Prend deux map et fusionne leurs résultats*)
let JoinMap map1 (map2:Map<string,int>) =
    Map.fold (fun acc key value -> if Map.containsKey key map2
                                                then Map.add key (value + map2.[key]) acc
                                                else Map.add key value acc) map2 map1

(*Ajoute le mot au résultat s'il est accepté par la fonction d'évaluation*)
let processSync texts evalFun =
    Seq.fold (fun acc x ->
                            if evalFun x then
                              if Map.containsKey x acc
                               then
                                let temp = acc.[x]
                                Map.add x (temp + 1) acc
                               else Map.add x 1 acc
                             else acc) Map.empty texts
                            
//Fait le traitement de tous les textes dans texts et joint les résultats
let processText texts evalFun =
    Seq.fold(fun acc x -> JoinMap acc (processSync x evalFun)) Map.empty texts

(*
    Cette méthode fait le map-Reduce, si le nombre de texte est plus grand que max Block, on subdivise et on fait deux appel récursif
    Params texts : Les textes qu'on a loadé
           maxBlock : Le nombre de textes a traité au maximum
           evalFun : Fonction d'évaluation pour savoir s'il faut compter le mot.
    Retour: Les mots comptés
 *)

let rec MapReduce texts maxBlock evalFun =
    async{
            if Seq.length texts <= maxBlock
            then return (processText texts evalFun)
            else 
                let offset = (Seq.length texts) / 2
                let skipList = Seq.skip offset texts
                //Start first Thread
                let! first = Async.StartChild (MapReduce (Seq.take offset texts) maxBlock evalFun)
                //Start second Thread
                let! second = Async.StartChild (MapReduce skipList  maxBlock evalFun)
                let! firstRep = first //Wait for first thread
                let! secondRep = second //Wait for second thread
                return (JoinMap firstRep secondRep) //Join both result
            }


(*Ecrit le Rapport et l'output, compte pas dans le temps de process
    Params : result : Les Résultats, dictionnaire string, int
             maxBlock : string disant le suffixe
             prefix : Prefix du fichier*)
let WriteReport result maxBlock prefix=
        let stream = new StreamWriter(prefix + "-" + maxBlock+ ".txt",true)
        let orderedByValue = result |> Map.toList
                                    |> List.sortBy (fun (key,v) -> -v)
        orderedByValue |> List.iter(fun (key,v) -> stream.WriteLine(key + "\t" + v.ToString()))
        stream.Close()
        Console.WriteLine("The most present word is : {0} and there is {1} occurrence",fst orderedByValue.Head,snd orderedByValue.Head)


[<EntryPoint>]
let main argv = 

    //Initialisation
    Console.Write("Loading files...   ")
    let files = seq {for i in 1 .. 2000 do yield "ift320-enonce-2-3-4.txt" }
    
    let sequence = seq {for i in 1 .. 10 do yield i * i}
    let texts = Seq.fold (fun acc x -> let text = System.IO.File.ReadAllText(x).Split([|'\n'; '\r'; ' '|],StringSplitOptions.RemoveEmptyEntries)
                                       text :: acc) [] files
    Console.WriteLine("Done")
    Console.WriteLine("Number of file : {0}", Seq.length files)

    //Async Stuff
    Console.WriteLine("Async")
    for i in 1 .. 10 do 
        //Trouve la taille acceptée des blocks
        let maxSize = 2.0**(i |> float) |> int
        Console.WriteLine("Size of chunk : {0}",maxSize)
        //Lance le timer
        let stopWatch = System.Diagnostics.Stopwatch.StartNew()
        //Lance le Map Reduce et attend le resultat
        let result = MapReduce texts maxSize EvalFun |>Async.Catch |> Async.RunSynchronously
        //Arrete le timer
        stopWatch.Stop()
        //Ici, on catch l'exception si il y en a une
        match result with
        | Choice1Of2 buffer -> WriteReport buffer (maxSize.ToString()) "Async"
        | Choice2Of2 exn -> Console.WriteLine("Une erreur est survenu OUPS")
        Console.WriteLine("Time Elapsed : {0} ms",stopWatch.Elapsed.TotalMilliseconds)
        //Ecrit le Report
        

    //Sync stuff
    Console.WriteLine("Sync")
    //part le timer
    let stopWatch = System.Diagnostics.Stopwatch.StartNew()
    //Process tous les fichiers un après l'autre
    let result = processText texts EvalFun
    //Arrete le timer
    stopWatch.Stop()
    //Écrit le report
    WriteReport result "" "Sync"
    Console.WriteLine("Time Elapsed : {0} ms",stopWatch.Elapsed.TotalMilliseconds)

    0 // return an integer exit code