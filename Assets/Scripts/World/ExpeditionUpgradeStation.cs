using SheltreSim.Interaction;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.World
{
    public sealed class ExpeditionUpgradeStation : MonoBehaviour, IInteractable
    {
        public string InteractPrompt => "Press E to upgrade expedition equipment";
        private Canvas canvas;
        private Text status, supplies;
        private Text[] descriptions = new Text[3];
        private Button[] buttons = new Button[3];
        private bool open, closeRequested;
        private InputAction cancel;
        private void Awake() => cancel = new InputAction("CloseUpgrades", InputActionType.Button, "<Keyboard>/escape");
        private void OnEnable() { cancel.Enable(); cancel.performed += OnCancel; }
        private void OnDisable() { Close(); cancel.performed -= OnCancel; cancel.Disable(); }
        private void OnCancel(InputAction.CallbackContext _) { if (open) closeRequested = true; }
        public void Interact(GameObject interactor)
        {
            var progression = ExpeditionProgression.Active;
            if (progression == null || (progression.GetComponent<SheltreSim.Survival.SurvivalStats>()?.IsDead ?? true)) return;
            if (!ModalUI.TryOpen(this, Close)) return;
            if (canvas == null) BuildUI();
            open = true;
            canvas.gameObject.SetActive(true);
            status.text = "Recover parts from landmarks. Explore further, return better equipped.";
            Refresh();
        }
        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform,"ExpeditionUpgrades",130,true);
            GameUI.Backdrop(canvas.transform,"Backdrop");
            var p = GameUI.Panel(canvas.transform,"Panel",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(880,700),GameUI.PanelColor).transform;
            GameUI.Label(p,"Title","EXPEDITION WORKSHOP",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-25),new Vector2(820,44),28,GameUI.Accent,TextAnchor.MiddleCenter);
            supplies = GameUI.Label(p,"Supplies","",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-82),new Vector2(820,45),20,Color.white,TextAnchor.MiddleCenter);
            for (int i=0;i<3;i++)
            {
                int index=i;
                var row=GameUI.Panel(p,"Upgrade_"+i,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-145-i*140),new Vector2(820,126),GameUI.RowColor).transform;
                descriptions[i]=GameUI.Label(row,"Description","",new Vector2(0,1),new Vector2(0,1),new Vector2(16,-5),new Vector2(600,118),16);
                buttons[i]=GameUI.Button(row,"Install","INSTALL",new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-16,0),new Vector2(185,50),()=>Buy(index));
            }
            status=GameUI.Label(p,"Status","",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,66),new Vector2(820,58),18,GameUI.Muted,TextAnchor.MiddleCenter);
            GameUI.Button(p,"Close","CLOSE [ESC]",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,14),new Vector2(240,42),Close);
        }
        private void Buy(int index)
        {
            ExpeditionProgression.Active.TryUpgrade(index,out string result);
            status.text=result;
            Refresh();
        }
        private void Refresh()
        {
            var p=ExpeditionProgression.Active;
            var inv=p.GetComponent<SheltreSim.Items.Inventory>();
            supplies.text=$"Landmarks discovered: {p.DiscoveryCount}   /   Expedition parts: {inv.GetCount(p.expeditionParts.id)}";
            for(int i=0;i<3;i++)
            {
                var cost=p.UpgradeCost(i);
                string text=ExpeditionProgression.UpgradeNames[i]+"\n"+ExpeditionProgression.UpgradeBenefits[i];
                if(!p.HasUpgrade(i))
                {
                    text+=$"\nCost: {cost[0].amount} expedition parts + {cost[1].amount} {cost[1].resource.displayName}; discover {i+1} sites";
                    var shortage=p.DescribeShortage(i);
                    text+=shortage.Count>0?"\n<color=#FFB85C>"+string.Join("\n",shortage)+"</color>":"\n<color=#A1DEA8>Ready to install.</color>";
                }
                descriptions[i].text=text;
                buttons[i].interactable=!p.HasUpgrade(i)&&p.DiscoveryCount>=i+1&&SheltreSim.Crafting.CraftingSystem.TryAfford(inv,cost);
                buttons[i].GetComponentInChildren<Text>().text=p.HasUpgrade(i)?"INSTALLED":"INSTALL";
            }
        }
        private void LateUpdate() { if(closeRequested) Close(); }
        public void Close()
        {
            if(!open)return;
            open=false;closeRequested=false;
            if(canvas!=null)canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }
        private void OnDestroy() => cancel?.Dispose();
    }
}
