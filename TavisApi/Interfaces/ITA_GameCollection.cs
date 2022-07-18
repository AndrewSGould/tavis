using static TavisApi.Services.TA_GameCollection;

namespace TavisApi.Services;

//https://www.trueachievements.com/gamecollection?executeformfunction&function=AjaxList&params=oGameCollection%7CoGameCollection_TimeZone=Eastern%20Standard%20Time%26txtGamerID%3D104571%26ddlSortBy%3DTitlename%26ddlDLCInclusionSetting%3DAllDLC%26ddlCompletionStatus%3DAll%26ddlTitleType%3DGame%26ddlContestStatus%3DAll%26asdGamePropertyID%3D-1%26oGameCollection_Order%3DDatecompleted%26oGameCollection_Page%3D1%26oGameCollection_ItemsPerPage%3D10000%26oGameCollection_ShowAll%3DFalse%26txtGameRegionID%3D2%26GameView%3DoptListView%26chkColTitlename%3DTrue%26chkColCompletionestincDLC%3DTrue%26chkColUnobtainables%3DTrue%26chkColSiteratio%3DTrue%26chkColPlatform%3DTrue%26chkColServerclosure%3DTrue%26chkColNotNotForContests%3DTrue%26chkColSitescore%3DTrue%26chkColOfficialScore%3DTrue%26chkColItems%3DTrue%26chkColDatestarted%3DTrue%26chkColDatecompleted%3DTrue%26chkColLastunlock%3DTrue%26chkColOwnershipstatus%3DTrue%26chkColPublisher%3DTrue%26chkColDeveloper%3DTrue%26chkColReleasedate%3DTrue%26chkColGamerswithgame%3DTrue%26chkColGamerscompleted%3DTrue%26chkColGamerscompletedperentage%3DTrue%26chkColCompletionestimate%3DTrue%26chkColSiterating%3DTrue%26chkColNotforcontests%3DTrue%26chkColInstallsize%3DTrue

public interface ITA_GameCollection {
  string ParseManager(int playerTrueAchId, int page);
  string ParseManager(int playerTrueAchId, int page, SyncOptions gameCollectionOptions);
}