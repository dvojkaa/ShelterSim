using System.Collections.Generic;
using System.Text;
using SheltreSim.Crafting;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>Contract and category shop for the pre-apocalypse bunker.</summary>
    public sealed class BunkerTerminalUI : MonoBehaviour
    {
        private static BunkerTerminalUI instance;
        public static bool IsOpen => instance != null && instance.open;

        private static readonly BunkerShop.Category[] Categories =
        {
            BunkerShop.Category.Supplies, BunkerShop.Category.Gear, BunkerShop.Category.Stations,
            BunkerShop.Category.PowerAndWater, BunkerShop.Category.Defence, BunkerShop.Category.Vehicles
        };
        private static readonly string[] CategoryNames = { "SUPPLIES", "GEAR", "STATIONS", "POWER & WATER", "DEFENCE", "VEHICLES" };

        private Canvas canvas;
        private Text balance, progress, contractText, sealText, status;
        private Button contractButton, sealButton;
        private readonly List<Text> roomTexts = new List<Text>();
        private readonly List<Button> roomButtons = new List<Button>();
        private readonly List<BunkerShop.Offer> roomOffers = new List<BunkerShop.Offer>();
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Text> tabLabels = new List<Text>();
        private RectTransform offerContent;
        private readonly List<OfferRow> offerRows = new List<OfferRow>();
        private InputAction cancel;
        private bool open, closeRequested, sealArmed;
        private float sealArmExpires;
        private int selectedCategory;

        private sealed class OfferRow
        {
            public BunkerShop.Offer offer;
            public Button button;
            public Text buttonText, details, effect, warning;
        }

        public static void Open(GameObject interactor)
        {
            if (instance == null)
            {
                var host = new GameObject("BunkerTerminalUI");
                instance = host.AddComponent<BunkerTerminalUI>();
            }
            instance.Show();
        }

        public static void CloseIfOpen()
        {
            if (instance != null && instance.open) instance.Close();
        }

        private void Awake()
        {
            cancel = new InputAction("CloseTerminal", InputActionType.Button, "<Keyboard>/escape");
            cancel.performed += _ => { if (open) closeRequested = true; };
            cancel.Enable();
        }

        private void OnDestroy()
        {
            cancel?.Dispose();
            if (instance == this) instance = null;
        }

        private void Show()
        {
            if (!ModalUI.TryOpen(this, Close)) return;
            if (canvas == null) BuildUI();
            open = true;
            sealArmed = false;
            canvas.gameObject.SetActive(true);
            status.text = "Sign the contract to unlock the catalogue. Orders are delivered to the Supply Cage.";
            Refresh();
        }

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "TerminalCanvas", 130, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            var tl = new Vector2(0f, 1f);
            var card = GameUI.Panel(canvas.transform, "Panel", Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, new Vector2(1420f, 920f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Title", "BUNKER PROJECT", tl, tl, new Vector2(24f, -18f), new Vector2(520f, 42f), 30, GameUI.Accent, bold: true);
            balance = GameUI.Label(card, "Balance", string.Empty, tl, tl, new Vector2(24f, -62f), new Vector2(1080f, 30f), 20);
            GameUI.Button(card, "Close", "Close [Esc]", Vector2.one, Vector2.one, new Vector2(-20f, -18f), new Vector2(170f, 44f), Close);

            Transform left = GameUI.Panel(card, "BunkerColumn", tl, tl, new Vector2(24f, -104f), new Vector2(520f, 744f), new Color(.07f,.105f,.11f)).transform;
            GameUI.Label(left, "Header", "THE BUNKER", tl, tl, new Vector2(14f,-12f), new Vector2(470f,30f), 19, GameUI.Accent, bold:true);
            Transform contract = GameUI.Panel(left, "Contract", tl, tl, new Vector2(12f,-48f), new Vector2(496f,92f), GameUI.RowColor).transform;
            contractText = GameUI.Label(contract, "Text", string.Empty, tl, tl, new Vector2(10f,-7f), new Vector2(340f,78f), 16);
            contractButton = GameUI.Button(contract, "Buy", "SIGN  $20,000", new Vector2(1f,.5f), new Vector2(1f,.5f), new Vector2(-8f,0f), new Vector2(136f,52f), BuyContract, true);
            progress = GameUI.Label(left, "Progress", string.Empty, tl, tl, new Vector2(14f,-146f), new Vector2(478f,110f), 16, GameUI.Muted);
            GameUI.Label(left, "RoomHeader", "ROOM LADDER  /  NEEDS CONCRETE SHELL", tl, tl, new Vector2(14f,-258f), new Vector2(480f,26f), 15, GameUI.Accent, bold:true);
            foreach (BunkerShop.Offer offer in BunkerShop.All)
            {
                if (offer.category != BunkerShop.Category.Rooms && offer.id != "door_reinforced") continue;
                int index = roomOffers.Count;
                roomOffers.Add(offer);
                Transform row = GameUI.Panel(left, "Room_" + offer.id, tl, tl,
                    new Vector2(12f, -290f - index * 62f), new Vector2(496f,56f), GameUI.RowColor).transform;
                roomTexts.Add(GameUI.Label(row, "Text", string.Empty, tl, tl, new Vector2(9f,-5f), new Vector2(330f,48f), 14));
                BunkerShop.Offer captured = offer;
                roomButtons.Add(GameUI.Button(row, "Buy", "$" + offer.price.ToString("N0"), new Vector2(1f,.5f), new Vector2(1f,.5f), new Vector2(-7f,0f), new Vector2(132f,42f), () => BuyOffer(captured)));
            }
            sealButton = GameUI.Button(left, "Seal", "SEAL THE BUNKER", tl, tl, new Vector2(12f,-672f), new Vector2(496f,58f), Seal, true);
            sealText = sealButton.GetComponentInChildren<Text>();

            Transform right = GameUI.Panel(card, "ShopColumn", tl, tl, new Vector2(560f,-104f), new Vector2(836f,744f), new Color(.07f,.105f,.11f)).transform;
            GameUI.Label(right, "ShopHeader", "PRE-APOCALYPSE SUPPLY CAGE", tl, tl, new Vector2(14f,-12f), new Vector2(790f,30f), 19, GameUI.Accent, bold:true);
            for (int i=0;i<Categories.Length;i++)
            {
                int index=i;
                float x=14f + i*132f;
                Button tab=GameUI.Button(right,"Tab"+i,CategoryNames[i],tl,tl,new Vector2(x,-48f),new Vector2(126f,42f),()=>SelectCategory(index));
                tabButtons.Add(tab);
                tabLabels.Add(tab.GetComponentInChildren<Text>());
            }
            offerContent=GameUI.ScrollContent(right,"Offers",new Vector2(12f,-100f),new Vector2(808f,600f));
            status=GameUI.Label(card,"Status",string.Empty,tl,tl,new Vector2(24f,-864f),new Vector2(1370f,42f),17,GameUI.Warning);
            BuildOfferRows();
        }

        private void BuildOfferRows()
        {
            foreach (Transform child in offerContent) Destroy(child.gameObject);
            offerRows.Clear();
            float y=0f;
            foreach (BunkerShop.Offer offer in BunkerShop.ForCategory(Categories[selectedCategory]))
            {
                Transform row=GameUI.Panel(offerContent,"Offer_"+offer.id,new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(0f,-y),new Vector2(790f,126f),GameUI.RowColor).transform;
                Text name=GameUI.Label(row,"Name",offer.displayName,new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(12f,-7f),new Vector2(580f,26f),18,Color.white,bold:true);
                Text details=GameUI.Label(row,"Details",string.Empty,new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(12f,-34f),new Vector2(580f,22f),14,GameUI.Muted);
                Text effect=GameUI.Label(row,"Effect",GrantSummary(offer),new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(12f,-56f),new Vector2(760f,36f),14,GameUI.Muted);
                Text warning=GameUI.Label(row,"DependencyWarning",string.Empty,new Vector2(0f,1f),new Vector2(0f,1f),new Vector2(12f,-96f),new Vector2(760f,22f),12,GameUI.Warning);
                BunkerShop.Offer captured=offer;
                Button button=GameUI.Button(row,"Buy","",Vector2.one,Vector2.one,new Vector2(-9f,-7f),new Vector2(164f,42f),()=>BuyOffer(captured));
                offerRows.Add(new OfferRow{offer=offer,button=button,buttonText=button.GetComponentInChildren<Text>(),details=details,effect=effect,warning=warning});
                y+=132f;
            }
            offerContent.sizeDelta=new Vector2(0f,y);
        }

        private static string GrantSummary(BunkerShop.Offer offer)
        {
            var sb=new StringBuilder();
            if(offer.grants!=null) foreach(BunkerShop.Grant grant in offer.grants)
            {
                if(sb.Length>0) sb.Append("  •  ");
                sb.Append(grant.quantity).Append(" × ").Append(grant.itemId.Replace('_',' '));
            }
            string craft=CraftHint(offer);
            if(!string.IsNullOrEmpty(craft)) sb.Append("   |   or craft: ").Append(craft);
            return sb.ToString();
        }

        private static string CraftHint(BunkerShop.Offer offer)
        {
            if(offer.grants==null) return string.Empty;
            foreach(BunkerShop.Grant grant in offer.grants)
            {
                string id=grant.itemId.StartsWith("kit_")?grant.itemId.Substring(4):grant.itemId;
                Recipe recipe=RecipeBook.Get(id);
                if(recipe==null) recipe=RecipeBook.Get("piece_"+id);
                Ingredient[] pieceInputs = null;
                RecipeBook.PieceCosts.TryGetValue(id, out pieceInputs);
                if(recipe==null && pieceInputs==null) continue;
                var sb=new StringBuilder();
                if(pieceInputs!=null)
                {
                    foreach(Ingredient input in pieceInputs)
                    {
                        if(sb.Length>0) sb.Append(", ");
                        sb.Append(input.amount).Append(' ').Append(input.id.Replace('_',' '));
                    }
                }
                else if(recipe.inputs!=null)
                {
                    foreach(Ingredient input in recipe.inputs)
                    {
                        if(sb.Length>0) sb.Append(", ");
                        sb.Append(input.amount).Append(' ').Append(input.id.Replace('_',' '));
                    }
                }
                return sb.ToString();
            }
            return string.Empty;
        }

        private void SelectCategory(int index)
        {
            selectedCategory=Mathf.Clamp(index,0,Categories.Length-1);
            BuildOfferRows();
            Refresh();
        }

        private void BuyContract() => Report(HomeBase.Active.TryBuyContract(out string result),result);
        private void BuyOffer(BunkerShop.Offer offer) => Report(HomeBase.Active.TryBuyOffer(offer,out string result),result);
        private void Seal()
        {
            HomeBase home=HomeBase.Active;
            if(home==null || !home.CanSeal) { Report(false,"Finish stages 1–3 before sealing the bunker."); return; }
            if(!sealArmed || Time.unscaledTime>sealArmExpires)
            {
                sealArmed=true;
                sealArmExpires=Time.unscaledTime+3f;
                status.text=$"Click again within 3 seconds to seal. Unspent ${home.UnspentSavings:N0} is lost.";
                sealText.text="CONFIRM SEAL";
                return;
            }
            sealArmed=false;
            Report(home.TrySeal(out string result),result);
        }

        private void Report(bool success,string result)
        {
            if(!success) SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny,.4f);
            sealArmed=false;
            status.text=result;
            Refresh();
        }

        private string BlockReason(HomeBase home,BunkerShop.Offer offer)
        {
            if(!home.CanShop || home.IsSealing) return "Shop closed";
            if(!home.HasContract && offer.id!="bunker_contract") return "Sign contract first";
            if(home.PurchaseCount(offer.id)>=offer.limit) return "Limit reached";
            if(home.CompletedStages<offer.requiredStage) return "Needs stage "+offer.requiredStage;
            if((home.Rooms & offer.requiredRooms)!=offer.requiredRooms) return "Needs required room";
            if(!string.IsNullOrEmpty(offer.exclusiveGroup))
                foreach(BunkerShop.Offer other in BunkerShop.All)
                    if(other.id!=offer.id && other.exclusiveGroup==offer.exclusiveGroup && (home.Rooms&other.roomBit)!=0) return "Other wing selected";
            if(home.Money<offer.price) return "Need $"+(offer.price-home.Money).ToString("N0");
            if(offer.grants!=null && offer.grants.Length>0 && home.supplyCage==null) return "Supply Cage unavailable";
            if(offer.grants!=null && home.supplyCage!=null)
                foreach(BunkerShop.Grant grant in offer.grants)
                    if(home.supplyCage.GetAddRoom(grant.itemId)<grant.quantity) return "Supply Cage full";
            return string.Empty;
        }

        private void Refresh()
        {
            HomeBase home=HomeBase.Active;
            if(home==null) return;
            balance.text=$"Savings ${home.Money:N0}     Spent ${home.Spent:N0}     Seal status: {(home.IsSealing?"LIFE SUPPORT IN PROGRESS":home.CanSeal?"READY":"NOT READY")}";
            contractText.text=home.HasContract?"Bunker Contract   /   SIGNED\nStarter pack delivered; stages 1–3 run automatically.":"Bunker Contract   /   mandatory first purchase\nPays for all four construction stages and grants the starter pack.";
            contractButton.interactable=!home.HasContract && home.CanShop && home.Money>=HomeBase.ContractPrice;
            contractButton.GetComponentInChildren<Text>().text=home.HasContract?"SIGNED":"SIGN  $20,000";
            string stageLine="";
            for(int i=0;i<HomeBase.Stages.Length;i++)
            {
                HomeBase.Stage stage=HomeBase.Stages[i];
                string state=home.CompletedStages>i?"DONE":home.IsBuilding&&home.CompletedStages==i?$"IN PROGRESS  {home.BuildFraction*100f:0}%":i<3&&home.HasContract?"SCHEDULED":$"LOCKED  ${stage.price:N0}";
                if(i==3 && home.CompletedStages==3) state="READY TO SEAL";
                stageLine+=$"{i+1}. {stage.name}  —  {state}\n";
            }
            progress.text=stageLine;
            for(int i=0;i<roomOffers.Count;i++)
            {
                BunkerShop.Offer offer=roomOffers[i];
                bool owned=(home.Rooms&offer.roomBit)!=0;
                string reason=BlockReason(home,offer);
                string roomHint=owned&&offer.id=="room_wing_hydro"?DependencyHint(home,offer.id):string.Empty;
                roomTexts[i].text=$"{offer.displayName}  /  {(owned?"OWNED":reason.Length==0?"AVAILABLE":reason)}"+
                    (roomHint.Length>0?"\n⚠ "+roomHint:string.Empty);
                roomTexts[i].color=roomHint.Length>0?GameUI.Warning:owned?GameUI.Accent:reason.Length==0?Color.white:GameUI.Muted;
                roomButtons[i].interactable=!owned&&reason.Length==0;
                roomButtons[i].GetComponentInChildren<Text>().text=owned?"OWNED":"$"+offer.price.ToString("N0");
            }
            foreach(OfferRow row in offerRows)
            {
                int count=home.PurchaseCount(row.offer.id);
                string reason=BlockReason(home,row.offer);
                row.details.text=$"${row.offer.price:N0}    {count}/{(row.offer.limit>=100000?"∞":row.offer.limit.ToString())} purchased"+(reason.Length>0?"    •  "+reason:"");
                row.details.color=reason.Length>0?GameUI.Warning:GameUI.Muted;
                row.effect.text=GrantSummary(row.offer);
                string dependencyHint=count>0?DependencyHint(home,row.offer.id):string.Empty;
                row.warning.text=dependencyHint.Length>0?"⚠ "+dependencyHint:string.Empty;
                row.warning.gameObject.SetActive(row.warning.text.Length>0);
                row.buttonText.text=reason.Length==0?"BUY  $"+row.offer.price.ToString("N0"):reason;
                row.button.interactable=reason.Length==0;
            }
            for(int i=0;i<tabButtons.Count;i++)
            {
                tabButtons[i].interactable=i!=selectedCategory;
                tabLabels[i].color=i==selectedCategory?GameUI.Accent:Color.white;
            }
            sealButton.interactable=home.CanSeal;
            if(!sealArmed) sealText.text="SEAL THE BUNKER";
            else if(Time.unscaledTime<=sealArmExpires) sealText.text=$"SEAL — ${home.UnspentSavings:N0} LOST";
            else { sealArmed=false; sealText.text="SEAL THE BUNKER"; }
        }

        private static string DependencyHint(HomeBase home,string offerId)
        {
            if(offerId!="mg_turret"&&offerId!="electric_well"&&offerId!="room_wing_hydro") return string.Empty;
            IReadOnlyList<string> hints=BunkerShop.DependencyHints(home);
            foreach(string hint in hints)
                if(hint.StartsWith("Needs power:",System.StringComparison.Ordinal) ||
                   hint.StartsWith("Needs fuel:",System.StringComparison.Ordinal)) return hint;
            return string.Empty;
        }

        private void Update(){if(open&&Time.frameCount%20==0)Refresh();}
        private void LateUpdate(){if(closeRequested)Close();}

        public void Close()
        {
            closeRequested=false;
            if(!open)return;
            open=false;
            if(canvas!=null)canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }
    }
}
