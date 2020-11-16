namespace Somnifero.ViewModels

open System.Collections.Generic


type HeaderData =
    { routeGroups: seq<IDictionary<string, string>> }

type FooterData =
    { routeGroups: seq<IDictionary<string, string>>
      extraData: Option<IDictionary<string, string>> }
