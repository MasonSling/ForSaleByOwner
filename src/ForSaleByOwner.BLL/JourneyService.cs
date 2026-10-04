using System.Globalization;

namespace ForSaleByOwner.BLL;

public sealed class JourneyService(ISaleWorkspaceStore store, ISampleDocumentCatalog samples)
{
    public SaleWorkspace Get(string session) => store.Read(session);
    public IReadOnlyList<SampleDocument> Samples => samples.All;
    public static string Value(JourneyState j,string key) => j.Answers.GetValueOrDefault(key)?.Value ?? "";
    public static bool Confirmed(JourneyState j,string key) => j.Answers.GetValueOrDefault(key) is { Status:"Confirmed" } a && !string.IsNullOrWhiteSpace(a.Value);
    private static void Audit(JourneyState j,string key,string before,string after,string reason) => j.History.Add(new(DateTime.UtcNow,key,before,after,reason));
    private SaleWorkspace Change(string session,int revision,Action<JourneyState> action) => store.Update(session,w => {
        if(w.Journey.Revision!=revision) throw new InvalidOperationException("Your workspace changed in another action or tab. Reload this step before saving; your newer work is safe.");
        action(w.Journey);
        w.Journey.Revision++;
        var j=w.Journey; var p=w.Property;
        p=p with { Address=Value(j,"address"),City=Value(j,"city"),Year=int.TryParse(Value(j,"year"),out var year)?year:p.Year,Hoa=Value(j,"hoa")=="Yes",Type=Value(j,"type"),Occupancy=Value(j,"occupancy"),Timing=Value(j,"timing"),HasBuyer=Value(j,"buyer")=="Yes" };
        return w with { Property=p,ListingCopy=Value(j,"description"),Published=j.Publication is not null };
    });
    public void Visit(string session,string section,string step) {
        JourneyDefinition.Step(section,step);
        store.Update(session,w=>{w.Journey.LastSection=section;w.Journey.LastStep=step;w.Journey.Visited.Add(section+"/"+step);return w;});
    }
    public void Save(string session,int revision,string section,string step,IReadOnlyDictionary<string,string> values,bool defer=false) {
        var definition=JourneyDefinition.Step(section,step);
        Change(session,revision,j=> {
            foreach(var field in definition.Fields) {
                if(defer && j.Answers.GetValueOrDefault(field.Key)?.Status=="Confirmed") continue;
                var value=(values.GetValueOrDefault(field.Key)??"").Trim();
                var unknown=defer || value=="Not sure" || value=="Not reviewed yet" || string.IsNullOrWhiteSpace(value);
                if(!unknown) Validate(field.Key,value);
                Put(j,field.Key,unknown && defer ? "" : value,unknown?"Unknown":"Confirmed","Seller confirmed this step");
            }
            if(defer) {
                j.Visited.Add(section+"/"+step);
                var deferredNext=JourneyDefinition.Neighbor(section,step,1);
                j.LastSection=deferredNext.Section;j.LastStep=deferredNext.Step;
                return;
            }
            if(section=="presentation"&&step=="description") {
                var approved=!defer&&values.GetValueOrDefault("approveDescription")=="yes"&&Confirmed(j,"description");
                Put(j,"descriptionApproved",approved?"yes":"",approved?"Confirmed":"Unknown","Description review");
            }
            if(step=="records") Put(j,"recordsReviewed",defer?"":"yes",defer?"Unknown":"Confirmed","Document collection reviewed; does not confirm pending findings");
            if(step=="requirements") {
                foreach(var key in PreparationKeys(j)) {
                    var v=values.GetValueOrDefault(key,"");
                    Put(j,key,!defer&&v=="yes"?"yes":"",!defer&&v=="yes"?"Confirmed":"Unknown","Preparation acknowledgement; no signature or delivery");
                }
                if(NeedsLead(j)) {
                    var lead=values.GetValueOrDefault("leadExemption")??"";
                    if(!new[]{"","No exemption identified","I need an exemption reviewed"}.Contains(lead)) throw new ArgumentException("Choose a listed lead-disclosure review option.");
                    Put(j,"leadExemption",defer?"":lead,!defer&&lead=="No exemption identified"?"Confirmed":"Unknown","Seller's exemption review, not a legal determination");
                }
            }
            if(step=="photos") Put(j,"photosReviewed",!defer&&j.Photos.Any(p=>p.Included)&&Confirmed(j,"cover")?"yes":"",!defer&&j.Photos.Any(p=>p.Included)&&Confirmed(j,"cover")?"Confirmed":"Unknown","Photo review");
            if(step=="check") Put(j,"finalReview",!defer&&Blockers(j).Count==0?"yes":"",!defer&&Blockers(j).Count==0?"Confirmed":"Unknown","Reviewed current listing draft");
            j.Visited.Add(section+"/"+step);
            var next=JourneyDefinition.Neighbor(section,step,1); j.LastSection=next.Section;j.LastStep=next.Step;
        });
    }
    private static void Validate(string key,string value) {
        if(value.Length>(key is "description" or "features" or "improvements" or "condition" or "priceNotes"?4000:160)) throw new ArgumentException("That answer is too long. Please shorten it.");
        var field=JourneyDefinition.Sections.SelectMany(s=>s.Steps).SelectMany(s=>s.Fields).FirstOrDefault(f=>f.Key==key);
        if(field?.Options is {} options&&!options.Contains(value)) throw new ArgumentException($"Choose a listed option for {field.Label}.");
        if(key=="type"&&value!="Single-family resale") throw new ArgumentException("This guided journey supports existing single-family homes in Texas. Your previous work is preserved; this property type needs a different workflow.");
        if(key=="state"&&value!="Texas") throw new ArgumentException("This guided journey currently supports Texas properties only.");
        if(key is "year" or "beds" or "baths" or "size" or "price" or "hoaFee") {
            if(!decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)||n<0) throw new ArgumentException($"Enter a valid number for {key}.");
            if(key=="year"&&(n<1800||n>DateTime.UtcNow.Year||n!=decimal.Truncate(n))) throw new ArgumentException("Enter a valid whole-number year built.");
            if(key=="beds"&&(n>50||n!=decimal.Truncate(n)) || key=="baths"&&n>50 || key=="size"&&(n<=0||n>100000) || key=="price"&&(n<=0||n>100000000)) throw new ArgumentException($"Check the value entered for {key}.");
        }
        if(key=="contactEmail"&&!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value)) throw new ArgumentException("Enter a valid sample email address.");
    }
    private static void Put(JourneyState j,string key,string value,string status,string reason,string source="Seller",string? doc=null,int? page=null) {
        var old=j.Answers.GetValueOrDefault(key);
        if(old?.Value==value && old.Status==status && source=="Seller" && old.DocumentId is not null) return;
        j.Answers[key]=new(value,status,source,doc,page);
        if(old?.Value==value&&old?.Status==status&&old?.DocumentId==doc) return;
        Audit(j,key,old is null?"Not answered":$"{old.Value} ({old.Status})",$"{value} ({status})",reason);
        void Stale(string target,string why) { if(j.Answers.TryGetValue(target,out var a)&&a.Status=="Confirmed") {j.Answers[target]=a with{Status="Needs confirmation"};Audit(j,target,a.Value,a.Value,why);} }
        if(key=="year") {Stale("leadReviewed","Year built changed; review lead guidance again.");Stale("leadExemption","Year built changed; review exemptions again.");}
        if(key=="hoa") Stale("hoaReviewed","HOA status changed; review association preparation again.");
        if((key is "address" or "city") && old?.Value!=value) {
            foreach(var d in j.Documents.Where(d=>d.Status!="Superseded").ToList()) {
                var index=j.Documents.FindIndex(x=>x.Id==d.Id); j.Documents[index]=d with{Status="Property changed — replace sample",Findings=d.Findings.Select(f=>f with{Status="Needs confirmation"}).ToList()};
            }
            foreach(var fact in j.Answers.Where(a=>a.Value.DocumentId is not null).Select(a=>a.Key).ToList()) Stale(fact,"Property address changed; source document needs review.");
        }
        var publicKeys=PublicKeys;
        if(publicKeys.Contains(key)||key is "description" or "cover" or "visibility" or "photosReviewed" or "contactEmail") {
            Stale("finalReview","The listing draft changed after review.");
            if(key is not "descriptionApproved" and not "visibility" and not "cover" and not "photosReviewed") Stale("descriptionApproved","Listing facts changed; review your description again.");
        }
    }
    public static readonly string[] PublicKeys=["address","city","type","year","beds","baths","size","features","price","showings","contactMethod"];
    public static bool NeedsLead(JourneyState j) => !Confirmed(j,"year") || int.TryParse(Value(j,"year"),out var y)&&y<1978;
    public static IEnumerable<string> PreparationKeys(JourneyState j) {
        yield return "disclosureReviewed";
        if(Value(j,"hoa")!="No"||!Confirmed(j,"hoa")) yield return "hoaReviewed";
        if(NeedsLead(j)) yield return "leadReviewed";
        yield return "titleReviewed";
    }
    public static List<string> Blockers(JourneyState j) {
        List<string> result=[];
        foreach(var key in PublicKeys.Where(k=>k!="features")) if(!Confirmed(j,key)) result.Add($"Confirm {Label(key)}.");
        foreach(var key in new[]{"state","county","ownership","sizeSource"}) if(!Confirmed(j,key)) result.Add($"Confirm {Label(key)}.");
        if(Value(j,"type")!="Single-family resale") result.Add("Confirm a supported single-family resale property.");
        if(!Confirmed(j,"description")||!Confirmed(j,"descriptionApproved")) result.Add("Review and approve your listing description.");
        if(!Confirmed(j,"cover")||!j.Photos.Any(p=>p.Id==Value(j,"cover")&&p.Included)||!Confirmed(j,"photosReviewed")) result.Add("Select a cover photo and review your photo selection.");
        if(Value(j,"visibility")!="I approve the selected public details"||!Confirmed(j,"visibility")) result.Add("Approve your public contact visibility.");
        if(Value(j,"contactMethod")=="Display a sample email"&&!Confirmed(j,"contactEmail")) result.Add("Confirm the sample email to display.");
        return result;
    }
    public static string Label(string key) => JourneyDefinition.Sections.SelectMany(s=>s.Steps).SelectMany(s=>s.Fields).FirstOrDefault(f=>f.Key==key)?.Label ?? key switch { "hoaFee"=>"Annual HOA assessment", "cover"=>"Cover photo", "recordsReviewed"=>"Document collection", "disclosureReviewed"=>"Seller disclosure preparation", "hoaReviewed"=>"HOA preparation", "leadReviewed"=>"Lead disclosure preparation", "leadExemption"=>"Lead exemption review", "titleReviewed"=>"Title and survey preparation", "descriptionApproved"=>"Description approval", "photosReviewed"=>"Photo selection review", "finalReview"=>"Final listing review", _=>key };
    public static string State(JourneyState j,string section) {
        var s=JourneyDefinition.Section(section);
        if(!j.Visited.Any(v=>v.StartsWith(section+"/"))) return "Not started";
        var keys=s.Steps.SelectMany(t=>t.Fields).Select(f=>f.Key).Where(k=>k is not "priceNotes" and not "contactEmail").ToList();
        if(section=="preferences"&&Value(j,"contactMethod")=="Display a sample email") keys.Add("contactEmail");
        if(section=="documents") {keys.AddRange(PreparationKeys(j));keys.Add("recordsReviewed");if(NeedsLead(j))keys.Add("leadExemption");}
        if(section=="presentation") keys.AddRange(["cover","photosReviewed","descriptionApproved"]);
        if(section=="review") return j.Publication is not null&&Confirmed(j,"finalReview")&&Blockers(j).Count==0?"Complete":Confirmed(j,"finalReview")?"Needs confirmation":"In progress";
        if(keys.Any(k=>j.Answers.GetValueOrDefault(k)?.Status=="Needs confirmation")||section=="documents"&&PendingFindings(j)>0) return "Needs confirmation";
        if(keys.All(k=>Confirmed(j,k))) return "Complete";
        return "In progress";
    }
    public static int PendingFindings(JourneyState j) => j.Documents.Where(d=>d.Status!="Superseded").Sum(d=>d.Findings.Count(f=>f.Status is "Needs confirmation" or "Not sure"));
    public string AddDocument(string session,int revision,string kind,bool replace) {
        var sample=samples.All.FirstOrDefault(s=>s.Kind==kind)??throw new ArgumentException("Choose a bundled sample.");
        var id=Guid.NewGuid().ToString("N");
        Change(session,revision,j=> {
            var old=j.Documents.Where(d=>d.Kind==kind).OrderByDescending(d=>d.Version).FirstOrDefault();
            if(old is not null&&!replace) throw new ArgumentException("This sample is already in your library. Open it, or choose Replace to create another version.");
            if(old is not null) {
                j.Documents[j.Documents.IndexOf(old)]=old with{Status="Superseded"};
                foreach(var key in j.Answers.Where(a=>a.Value.DocumentId==old.Id).Select(a=>a.Key).ToList()) {var a=j.Answers[key];j.Answers[key]=a with{Status="Needs confirmation"};Audit(j,key,a.Value,a.Value,"Source document replaced; reconfirm against the new version.");}
            }
            j.Documents.Add(new(id,kind,sample.Filename,(old?.Version??0)+1,"Processing",sample.Path,sample.Sections,[]));
            Audit(j,kind,old?.Id??"None",id,"Added bundled sample version; analysis is simulated.");
        });
        return id;
    }
    public void ProcessDocument(string session,int revision,string id) => Change(session,revision,j=> {
        var d=j.Documents.FirstOrDefault(d=>d.Id==id)??throw new ArgumentException("Document not found.");
        if(d.Status!="Processing") throw new ArgumentException("This document is no longer waiting for processing.");
        var sample=samples.All.Single(s=>s.Kind==d.Kind);
        var status=sample.Failure??"Needs confirmation";
        // Bundled content belongs to this fictional address; never relabel it as another property.
        if(sample.Failure is null&&(Value(j,"address")!="1842 Live Oak Lane"||Value(j,"city")!="Austin")) status="Wrong property";
        j.Documents[j.Documents.IndexOf(d)]=d with{Status=status,Findings=status=="Needs confirmation"?sample.Findings.ToList():[]};
        Audit(j,d.Filename,"Processing",status,"Deterministic sample analysis");
    });
    public void ReviewFinding(string session,int revision,string document,string finding,string decision,string? corrected) => Change(session,revision,j=> {
        var d=j.Documents.FirstOrDefault(d=>d.Id==document)??throw new ArgumentException("Document not found.");
        if(d.Status is not "Needs confirmation" and not "Reviewed") throw new ArgumentException("This version cannot be reviewed. Choose a readable sample for this property.");
        var f=d.Findings.FirstOrDefault(f=>f.Id==finding)??throw new ArgumentException("Finding not found.");
        if(!new[]{"confirm","edit","unsure"}.Contains(decision)) throw new ArgumentException("Choose confirm, edit, or not sure.");
        var value=decision=="edit"?(corrected??"").Trim():f.Value;
        if(decision!="unsure") {if(string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter the corrected value.");Validate(f.Field,value);Put(j,f.Field,value,"Confirmed",decision=="edit"?"Seller corrected extracted finding":"Seller confirmed extracted finding",decision=="edit"?"Seller correction to sample":d.Filename,d.Id,f.Page);}
        var findings=d.Findings.Select(x=>x.Id==f.Id?x with{Status=decision=="unsure"?"Not sure":"Confirmed"}:x).ToList();
        j.Documents[j.Documents.IndexOf(d)]=d with{Findings=findings,Status=findings.All(x=>x.Status=="Confirmed")?"Reviewed":"Needs confirmation"};
        Audit(j,d.Filename+":"+f.Field,f.Status,decision,"Reviewed source finding; prior decisions retained.");
    });
    public void Photo(string session,int revision,string id,string operation,string? caption) => Change(session,revision,j=> {
        var p=j.Photos.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("Photo not found.");var index=j.Photos.IndexOf(p);
        switch(operation) {
            case "cover": if(!p.Included) throw new ArgumentException("Include this photo before choosing it as cover.");Put(j,"cover",id,"Confirmed","Selected cover");break;
            case "toggle":j.Photos[index]=p with{Included=!p.Included};if(Value(j,"cover")==id&&!j.Photos[index].Included)Put(j,"cover","","Unknown","Cover photo removed");break;
            case "earlier":if(index>0)(j.Photos[index-1],j.Photos[index])=(j.Photos[index],j.Photos[index-1]);break;
            case "later":if(index<j.Photos.Count-1)(j.Photos[index+1],j.Photos[index])=(j.Photos[index],j.Photos[index+1]);break;
            case "caption":if((caption??"").Length>160)throw new ArgumentException("Keep captions under 160 characters.");j.Photos[index]=p with{Caption=caption?.Trim()??""};break;
            default:throw new ArgumentException("Unknown photo action.");
        }
        Put(j,"photosReviewed","","Needs confirmation","Photo selection changed; review again.");
        Audit(j,"photos",p.Id,operation,"Photo organization updated");
    });
    public void Ask(string session,string section,string step,string prompt) {
        JourneyDefinition.Step(section,step);
        if(string.IsNullOrWhiteSpace(prompt)||prompt.Length>1000)throw new ArgumentException("Ask a question of up to 1,000 characters.");
        store.Update(session,w=> {
            var j=w.Journey;
            if(!j.Conversations.TryGetValue(section,out var conversation))j.Conversations[section]=conversation=[];
            string reply;
            if(section=="presentation"&&prompt.Contains("draft",StringComparison.OrdinalIgnoreCase)) {
                var keys=new[]{"address","city","year","beds","baths","size"};
                if(keys.Any(k=>!Confirmed(j,k)))reply="Before I draft your listing, confirm the address and home facts. I won't fill in missing details or use private condition notes.";
                else {
                    var draft=$"Explore this {Value(j,"beds")}-bedroom, {Value(j,"baths")}-bath single-family home at {Value(j,"address")} in {Value(j,"city")}, Texas. Built in {Value(j,"year")}, the home offers {Value(j,"size")} square feet of living area.";
                    if(Confirmed(j,"features"))draft+=$" Seller-confirmed features: {Value(j,"features")}.";
                    j.Proposals.Add(new(Guid.NewGuid().ToString("N"),section,"description",draft,"Drafted only from confirmed public property facts.",j.Revision));
                    reply="I've prepared a draft for you to review below. Accepting it fills the description; you'll still review and approve the final copy before publishing.";
                }
            } else if(section=="documents") {
                var d=j.Documents.LastOrDefault(d=>d.Status=="Needs confirmation");
                reply=$"You recorded HOA status as '{Value(j,"hoa")}' in Your property. {(NeedsLead(j)?"The pre-1978 lead step also needs review; confirm the build year and review the federal exemptions in the linked EPA guidance.":"Your confirmed build year is 1978 or later, so the federal pre-1978 step is not included.")} "+(d is null?"Start with a bundled sample. I will propose facts for confirmation, not silently save them.":$"Your {d.Filename}, version {d.Version}, has findings to review. Open it to compare each value with the cited source page.");
            } else if(section=="preferences") reply="The asking price is your decision. Compare reliable local sales evidence or ask a qualified professional; this demo has no valuation feed. Your pricing notes stay private. Only the contact method you approve appears in the listing.";
            else if(section=="details") reply=$"We're working on {JourneyDefinition.Step(section,step).Title} Your recorded build year is '{Value(j,"year")}' ({j.Answers.GetValueOrDefault("year")?.Status ?? "Unknown"}). Use a sample property record or your own confirmed information. Choose 'I don't know yet' instead of guessing; I'll keep the missing facts visible.";
            else if(section=="review") reply=Blockers(j).Count==0?"Your public listing prerequisites are ready. Review the snapshot and explicitly approve publication. Unfinished disclosure or title preparation remains on its own track; it is not marked complete by publishing.":"Before publication: "+string.Join(" ",Blockers(j));
            else reply=$"I'm helping with {JourneyDefinition.Section(section).Title}, at '{JourneyDefinition.Step(section,step).Title}' Your current property is {Value(j,"address")}, {Value(j,"city")}. Confirm the answers you know, or save an unknown and return later. I preserve the other sections when you make changes here.";
            conversation.Add(new("seller",prompt));conversation.Add(new("agent",reply));
            return w;
        });
    }
    public void AcceptProposal(string session,int revision,string id) => Change(session,revision,j=> {
        var p=j.Proposals.FirstOrDefault(p=>p.Id==id)??throw new ArgumentException("Proposal not found.");
        if(p.BasedOnRevision!=j.Revision||p.Status!="Needs confirmation")throw new InvalidOperationException("The draft context changed. Ask for a fresh proposal before accepting.");
        Put(j,p.Field,p.Value,"Confirmed","Accepted agent proposal","Agent draft reviewed by seller");
        Put(j,"descriptionApproved","","Unknown","Review the accepted draft before approving it");
        j.Proposals[j.Proposals.IndexOf(p)]=p with{Status="Accepted"};
    });
    public void Publish(string session,int revision,bool approved) => Change(session,revision,j=> {
        if(!approved)throw new ArgumentException("Explicitly approve the exact listing preview before publishing.");
        if(Blockers(j).Count>0||!Confirmed(j,"finalReview"))throw new ArgumentException("Finish the listing readiness items and final review before publishing.");
        var facts=PublicKeys.Where(k=>Confirmed(j,k)).ToDictionary(k=>k,k=>Value(j,k));
        if(Value(j,"contactMethod")=="Display a sample email")facts["contactEmail"]=Value(j,"contactEmail");
        j.Publication=new(DateTime.UtcNow,j.Revision+1,facts,Value(j,"description"),j.Photos.Where(p=>p.Included).ToList(),Value(j,"cover"));
        Audit(j,"publication", "Previous snapshot",$"Revision {j.Revision+1}","Seller explicitly approved this public demo snapshot");
    });
}
