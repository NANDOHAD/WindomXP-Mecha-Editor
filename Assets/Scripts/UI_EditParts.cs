using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using RuntimeHandle;
public class UI_EditParts : MonoBehaviour
{
    public RoboStructure robo;
    public RoboStructure prevRobo;
    public UI_EditAni ea;

    [Header("Parts List")]
    public int index = 0;
    List<string> list = new List<string>();
    List<GameObject> items = new List<GameObject>();
    List<bool> selected = new List<bool>();
    public GameObject Template;
    public Color selectedColor;
    public Color deselectedColor;

    [Header("Transform Editing")]
    public InputField PosX;
    public InputField PosY;
    public InputField PosZ;
    public InputField RotX;
    public InputField RotY;
    public InputField RotZ;
    public InputField ScaleX;
    public InputField ScaleY;
    public InputField ScaleZ;
    public RuntimeTransformHandle handle;
    public Space space = Space.Self;
    int moveType = 0;
    Vector3 mousePosition = Vector3.zero;
    public FreeCam fc;
    public bool lockHandle = false;
    public bool disableGOUpdates = false;
    public Toggle syncContraints;
    
    [Header("Copy/Paste")]
    public hod2v1 cpHod;
    public bool[] cpSelected;
    public int cpIndex;

    [Header("Add/Remove Parts")]
    public GameObject addPartsPanel;
    public Dropdown addPartsList;
    public Text addText;
    public UI_MsgBox msgBox;
    public UI_InputBox inputBox;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!disableGOUpdates && ea.cAnim.frames.Count > 0 && Input.GetMouseButton(0))
        {
            TransformTextUpdate();
            robo.updatePart(ea.animDD.value, ea.hodDD.value, index, syncContraints.isOn);
        }
    }

    public void PopulatePartsList()
    {
        clear();
        for (int i = 0; i < robo.parts.Count; i++)
        {
            string offset = "";
            for (int j = 0; j < robo.hod.parts[i].treeDepth; j++)
                offset += "   ";
            addItem(robo.parts[i],offset + "|_" + robo.parts[i].name);
        }
    }

    public void SelectedIndexChanged(int _index)
    {
        handle.target = robo.parts[_index].transform;
        if (index == _index)
            return;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            index = _index;
            selected[_index] = true;
        }
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            int[] range = new int[2];
            if (_index > index)
            {
                range[0] = index;
                range[1] = _index;
            }
            else
            {
                range[0] = _index;
                range[1] = index;
            }


            for (int i = 0; i < list.Count; i++)
            {
                if (i >= range[0] && i <= range[1])
                    selected[i] = true;
                else
                    selected[i] = false;
            }

            index = _index;
        }
        else
        {
            index = _index;
            for (int i = 0; i < list.Count; i++)
            {
                if (_index == i)
                    selected[i] = true;
                else
                    selected[i] = false;
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            ColorBlock cb = items[i].GetComponent<Button>().colors;
            if (selected[i])
                cb.normalColor = selectedColor;
            else
                cb.normalColor = deselectedColor;
            items[i].GetComponent<Button>().colors = cb;
        }
        TransformTextUpdate();
    }

    public void clear()
    {
        foreach (var item in items)
            GameObject.Destroy(item);

        list.Clear();
        items.Clear();
    }

    public void addItem(GameObject prt, string item)
    {
        list.Add(item);
        GameObject GO = GameObject.Instantiate(Template, Template.transform.parent);
        items.Add(GO);
        selected.Add(false);
        Button b = GO.GetComponent<Button>();
        int lPos = list.Count - 1;
        b.onClick.AddListener(() => { SelectedIndexChanged(lPos); });
        Toggle tg = GO.GetComponentInChildren<Toggle>();
        if (tg != null)
            tg.onValueChanged.AddListener((bool value) => { if (prt.GetComponent<MeshRenderer>() != null) prt.GetComponent<MeshRenderer>().enabled = value; });

        Text t = GO.transform.GetChild(0).GetComponent<Text>();
        t.text = item;
        GO.SetActive(true);
    }

    public void PositionCursor()
    {
        handle.type = HandleType.POSITION;

    }

    public void RotationCursor()
    {
        handle.type = HandleType.ROTATION;
    }


    public void HundleChange(int s)
    {
        if (s == 0)
            handle.type = HandleType.POSITION;
        if (s == 1)
            handle.type = HandleType.ROTATION;
        if (s == 2)
            handle.type = HandleType.SCALE;
    }

    public void spaceChange(int s)
    {
        if (s == 0)
            handle.space = HandleSpace.WORLD;
        else
            handle.space = HandleSpace.LOCAL;

        space = (Space)s;
        TransformTextUpdate();
    }
    public void TransformTextUpdate()
    {
        if (space == Space.Self)
        {
            PosX.text = robo.parts[index].transform.localPosition.x.ToString();
            PosY.text = robo.parts[index].transform.localPosition.y.ToString();
            PosZ.text = robo.parts[index].transform.localPosition.z.ToString();
            RotX.text = robo.parts[index].transform.localRotation.eulerAngles.x.ToString();
            RotY.text = robo.parts[index].transform.localRotation.eulerAngles.y.ToString();
            RotZ.text = robo.parts[index].transform.localRotation.eulerAngles.z.ToString();
            
        }
        else
        {
            PosX.text = robo.parts[index].transform.position.x.ToString();
            PosY.text = robo.parts[index].transform.position.y.ToString();
            PosZ.text = robo.parts[index].transform.position.z.ToString();
            RotX.text = robo.parts[index].transform.rotation.eulerAngles.x.ToString();
            RotY.text = robo.parts[index].transform.rotation.eulerAngles.y.ToString();
            RotZ.text = robo.parts[index].transform.rotation.eulerAngles.z.ToString();
            

        }

        ScaleX.text = robo.parts[index].transform.localScale.x.ToString();
        ScaleY.text = robo.parts[index].transform.localScale.y.ToString();
        ScaleZ.text = robo.parts[index].transform.localScale.z.ToString();

        if (robo.ani != null)
            ea.setConstraintText();

    }

    public void TransformValueUpdate() //sync the data from the text boxes into the part selected
    {
        if (!disableGOUpdates)
        {
            hod2v1_Part prt = new hod2v1_Part();
            Vector3 position = new Vector3();
            if (float.TryParse(PosX.text, out position.x) &&
                float.TryParse(PosY.text, out position.y) &&
                float.TryParse(PosZ.text, out position.z))
            {
                prt.position = position;
            }


            Vector3 euler = new Vector3();
            if (float.TryParse(RotX.text, out euler.x) &&
                float.TryParse(RotY.text, out euler.y) &&
                float.TryParse(RotZ.text, out euler.z))
            {
                prt.rotation = Quaternion.Euler(euler);
            }

            Vector3 scale = new Vector3();
            if (float.TryParse(ScaleX.text, out scale.x) &&
                float.TryParse(ScaleY.text, out scale.y) &&
                float.TryParse(ScaleZ.text, out scale.z))
                prt.scale = scale;
            if (robo.ani != null)
                robo.updatePart(ea.animDD.value, ea.hodDD.value, index, prt, space);
            else
                robo.updatePart(index, prt, space);
            

        }
    }

    public void copyValues()
    {
        cpHod = robo.createHod2v1();
        cpIndex = index;
        cpSelected = selected.ToArray();

    }

    public void pasteValues()
    {
        int count = selected.FindAll(x => x == true).Count;
        Debug.Log(count);
        if (count > 1)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (cpSelected[i])
                {
                    robo.updatePart(i, cpHod.parts[i]);
                }
            }
        }
        else
        {
            robo.updatePart(index, cpHod.parts[cpIndex]);
        }

        TransformTextUpdate();

        
    }

    public void addParts()
    {
        addPartsPanel.SetActive(true);
        addText.text = "New Part Parent will be " + robo.hod.parts[index].name;
        DirectoryInfo di = new DirectoryInfo(robo.folder);
        FileInfo[] files = di.GetFiles();
        List<string> fName = new List<string>();
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Extension == ".x")
                fName.Add(files[i].Name);
        }
        addPartsList.ClearOptions();
        addPartsList.AddOptions(fName);
    }

    public void addParts2()
    {
        //update internal hod if ani isn't loaded
        robo.addPart(addPartsList.options[addPartsList.value].text, index);
        if (robo.ani != null)
        {
            prevRobo.buildStructure(robo.ani.structure);
            foreach (GameObject prt in prevRobo.parts)
            {
                prt.layer = 7;
            }
        }
        addPartsPanel.SetActive(false);
        PopulatePartsList();
        robo.setPose(ea.animDD.value, ea.hodDD.value);
    }

    public void addPartsCancel()
    {
        addPartsPanel.SetActive(false);
    }

    public void removePart()
    {
        if (!robo.removePart(index))
        {
            msgBox.Show("You can't delete parts with children");
        }
        else
        {
            if (robo.ani != null)
            {
                prevRobo.buildStructure(robo.ani.structure);
                foreach (GameObject prt in prevRobo.parts)
                {
                    prt.layer = 7;
                }
            }

            PopulatePartsList();
            robo.setPose(ea.animDD.value, ea.hodDD.value);
        }
        
    }

    public void renamePart()
    {
        inputBox.openDialog("What part do you want to change/rename this too.  part must exist inorder for it to be shown.", robo.hod.parts[index].name, (string rText) =>
        {
            if (!File.Exists(Path.Combine(robo.folder, rText)))
                msgBox.Show("This part does not exist in the files");

            robo.renamePart(index, rText);
            if (robo.ani != null)
            {
                prevRobo.buildStructure(robo.ani.structure);
                foreach (GameObject prt in prevRobo.parts)
                {
                    prt.layer = 7;
                }
            }
            PopulatePartsList();
            robo.setPose(ea.animDD.value, ea.hodDD.value);
        });
    }
}
