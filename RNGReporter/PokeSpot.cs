﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RNGReporter.Objects;
using System.Globalization;

namespace RNGReporter.Objects
{
    public partial class PokeSpot : Form
    {
        private readonly String[] Natures = { "Hardy", "Lonely", "Brave", "Adamant", "Naughty", "Bold", "Docile", "Relaxed", "Impish", "Lax", "Timid", "Hasty", "Serious", "Jolly", "Naive", "Modest", "Mild", "Quiet", "Bashful", "Rash", "Calm", "Gentle", "Sassy", "Careful", "Quirky" };
        private Thread searchThread;
        private bool refresh;
        private List<PokeSpotDisplay> displayList;
        private ThreadDelegate gridUpdate;
        private BindingSource binding = new BindingSource();
        private bool isSearching = false;
        private uint shinyval;
        private bool abort = false;
        private List<uint> natureList;
        private List<uint> spotList;

        public PokeSpot()
        {
            InitializeComponent();
        }

        public PokeSpot(int TID, int SID)
        {
            InitializeComponent();
            abilityType.SelectedIndex = 0;
            genderType.SelectedIndex = 0;
            id.Text = TID.ToString();
            sid.Text = SID.ToString();
            dataGridViewResult.AutoGenerateColumns = false;
            abort = false;
        }

        private void PokeSpot_Load(object sender, EventArgs e)
        {
            comboBoxNature.Items.AddRange(Nature.NatureDropDownCollectionSearchNatures());
            comboBoxSpotType.Items.AddRange(addSpotTypes());
            setComboBox();
        }

        private void setComboBox()
        {
            comboBoxNature.CheckBoxItems[0].Checked = true;
            comboBoxNature.CheckBoxItems[0].Checked = false;
            comboBoxSpotType.CheckBoxItems[0].Checked = true;
            comboBoxSpotType.CheckBoxItems[0].Checked = false;
        }

        private void PokeSpot_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (searchThread != null)
                searchThread.Abort();
            abort = true;
        }

        private void search_Click(object sender, EventArgs e)
        {
            if (isSearching)
            {
                status.Text = "Previous search is still running";
                return;
            }

            dataGridViewResult.Rows.Clear();

            isSearching = true;
            displayList = new List<PokeSpotDisplay>();
            binding = new BindingSource { DataSource = displayList };
            dataGridViewResult.DataSource = binding;
            status.Text = "Searching";
            shinyval = (uint.Parse(id.Text) ^ uint.Parse(sid.Text)) >> 3;

            uint seed = 0;
            uint.TryParse(textBoxSeed.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed);
            uint frame = 3000000;
            uint.TryParse(maxFrame.Text, out frame);

            natureList = null;
            if (comboBoxNature.Text != "Any" && comboBoxNature.CheckBoxItems.Count > 0)
                natureList = (from t in comboBoxNature.CheckBoxItems where t.Checked select (uint)((Nature)t.ComboBoxItem).Number).ToList();

            spotList = null;
            List<uint> temp = new List<uint>();
            if (comboBoxSpotType.Text != "Any" && comboBoxSpotType.CheckBoxItems.Count > 0)
                for (int x = 1; x < 5; x++)
                    if (comboBoxSpotType.CheckBoxItems[x].Checked)
                        temp.Add((uint)(x - 1));

            if (temp.Count != 0)
                spotList = temp;

            uint gender = (uint)genderType.SelectedIndex;
            uint ability = (uint)abilityType.SelectedIndex;
            bool shinyCheck = Shiny_Check.Checked;

            searchThread = new Thread(() => searchPokeSpot(seed, frame, gender, ability, shinyCheck));
            searchThread.Start();

            var update = new Thread(updateGUI);
            update.Start();
        }

        private void searchPokeSpot(uint seed, uint frame, uint gender, uint ability, bool shinyCheck)
        {
            for (uint x = 1; x < frame + 1; x++)
            {
                filterPokeSpot(seed, x, gender, ability, shinyCheck);
                seed = forward(seed);
                if ((x & 0xF) == 0)
                    refresh = true;
            }
            isSearching = false;
            status.Invoke((MethodInvoker)(() => status.Text = "Done. - Awaiting Command"));
        }

        private void filterPokeSpot(uint seed, uint frame, uint gender, uint ability, bool shinyCheck)
        {
            uint rng3 = forward(forward(forward(forward(seed))));
            uint rng4 = forward(rng3);
            uint pid = ((rng3 >> 16) << 16) + (rng4 >> 16);

            uint nature = pid == 0 ? 0 : pid - 25 * (pid / 25);
            if (natureList != null)
            {
                if (!natureList.Contains(nature))
                    return;
            }

            String shiny = "";
            if (shinyCheck)
            {
                uint shinyNum = ((pid >> 16) ^ (pid & 0xFFFF)) >> 3;
                if (shinyNum != shinyval)
                    return;
                shiny = "!!!";
            }

            if (ability != 0)
            {
                uint actualAbility = pid & 1;
                if (actualAbility != (ability - 1))
                    return;
            }

            if (gender != 0)
            {
                if (gender == 1)
                {
                    if ((pid & 255) < 127)
                    {
                        return;
                    }
                }
                else if (gender == 2)
                {
                    if ((pid & 255) > 126)
                    {
                        return;
                    }
                }
                else if (gender == 3)
                {
                    if ((pid & 255) < 191)
                    {
                        return;
                    }
                }
                else if (gender == 4)
                {
                    if ((pid & 255) > 190)
                    {
                        return;
                    }
                }
                else if (gender == 5)
                {
                    if ((pid & 255) < 64)
                    {
                        return;
                    }
                }
                else if (gender == 6)
                {
                    if ((pid & 255) > 63)
                    {
                        return;
                    }
                }
                else if (gender == 7)
                {
                    if ((pid & 255) < 31)
                    {
                        return;
                    }
                }
                else if (gender == 8)
                {
                    if ((pid & 255) > 30)
                    {
                        return;
                    }
                }
            }

            calcPokeSpot(seed, pid, frame, nature, gender, ability, shiny);

        }

        private void calcPokeSpot(uint seed, uint pid, uint frame, uint nature, uint gender, uint ability, String shiny)
        {
            uint call1 = forward(seed);
            call1 >>= 16;
            uint currentCall = call1 == 0 ? 0 : call1 - 3 * (call1 / 3);

            if (currentCall == 0)
            {
                String spotType = "";
                uint call2 = forward(forward(seed));
                uint call3 = forward(call2);
                call2 >>= 16;
                call3 >>= 16;

                if (shiny == "")
                {
                    uint shinyNum = ((pid >> 16) ^ (pid & 0xFFFF)) >> 3;
                    if (shinyNum == shinyval)
                        shiny = "!!!";
                }

                currentCall = call3 == 0 ? 0 : call3 - 100 * (call3 / 100);

                if (spotList != null)
                {
                    foreach (uint x in spotList)
                    {
                        if (x == 0)
                        {
                            if ((call2 == 0 ? 0 : call2 - 100 * (call2 / 100)) > 9)
                                if (currentCall < 50)
                                    spotType = "Common";
                        }
                        else if (x == 1)
                        {
                            if ((call2 == 0 ? 0 : call2 - 100 * (call2 / 100)) > 9)
                                if (currentCall > 49 && currentCall < 85)
                                    spotType = "Uncommon";
                        }
                        else if (x == 2)
                        {
                            if ((call2 == 0 ? 0 : call2 - 100 * (call2 / 100)) > 9)
                                if (currentCall > 84)
                                    spotType = "Rare";
                        }
                        else
                        { 
                        if ((call2 == 0 ? 0 : call2 - 100 * (call2 / 100)) < 10)
                            spotType = "Munchlax";
                        }
                    }
                    if (spotType.Equals(""))
                        return;
                }
                else
                {
                    if ((call2 == 0 ? 0 : call2 - 100 * (call2 / 100)) < 10)
                        spotType = "Munchlax";
                    else if (currentCall < 50)
                        spotType = "Common";
                    else if (currentCall > 49 && currentCall < 85)
                        spotType = "Uncommon";
                    else
                        spotType = "Rare";
                }

                String stringNature = Natures[nature];
                gender = pid & 255;
                char gender1;
                char gender2;
                char gender3;
                char gender4;
                ability = pid & 1;

                gender1 = gender < 31 ? 'F' : 'M';
                gender2 = gender < 64 ? 'F' : 'M';
                gender3 = gender < 126 ? 'F' : 'M';
                gender4 = gender < 191 ? 'F' : 'M';

                displayList.Add(new PokeSpotDisplay
                {
                    Seed = seed.ToString("x").ToUpper(),
                    Frame = (int)frame,
                    PID = pid.ToString("x").ToUpper(),
                    Shiny = shiny,
                    Type = spotType,
                    Nature = stringNature,
                    Ability = (int)ability,
                    Eighth = gender1,
                    Quarter = gender2,
                    Half = gender3,
                    Three_Fourths = gender4
                });

            }
        }

        private uint forward(uint seed)
        {
            return (seed * 0x343FD + 0x269EC3) & 0xFFFFFFFF;
        }

        private uint reverse(uint seed)
        {
            return (seed * 0xB9B33155 + 0xA170F641) & 0xFFFFFFFF;
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            if (isSearching)
            {
                isSearching = false;
                status.Text = "Cancelled. - Awaiting Command";
                searchThread.Abort();
            }
        }

        private void updateGUI()
        {
            gridUpdate = dataGridUpdate;
            ThreadDelegate resizeGrid = dataGridViewResult.AutoResizeColumns;
            try
            {
                bool alive = true;
                while (alive)
                {
                    if (refresh)
                    {
                        Invoke(gridUpdate);
                        refresh = false;
                    }
                    if (searchThread == null || !searchThread.IsAlive)
                    {
                        alive = false;
                    }

                    Thread.Sleep(500);
                }
            }
            finally
            {
                if (!abort)
                {
                    Invoke(gridUpdate);
                    Invoke(resizeGrid);
                }
            }
        }


        #region Nested type: ThreadDelegate

        private delegate void ThreadDelegate();

        #endregion

        private void dataGridUpdate()
        {
            binding.ResetBindings(true);
        }

        private void anyPokeSpot_Click(object sender, EventArgs e)
        {
            comboBoxSpotType.ClearSelection();
        }

        private void anyNature_Click(object sender, EventArgs e)
        {
            comboBoxNature.ClearSelection();
        }

        private void anyGender_Click(object sender, EventArgs e)
        {
            genderType.SelectedIndex = 0;
        }

        private void anyAbility_Click(object sender, EventArgs e)
        {
            abilityType.SelectedIndex = 0;
        }

        private String[] addSpotTypes()
        {
            String[] temp = new String[]
                {
                    "Common",
                    "Uncommon",
                    "Rare",
                    "Munchlax"
                };

            return temp;
        }
    }
}