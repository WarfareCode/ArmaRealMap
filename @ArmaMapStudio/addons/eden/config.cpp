#include "script_component.hpp"

class CfgPatches {
    class ADDON {
        name = QUOTE(COMPONENT);
        units[] = {};
        weapons[] = {};
        requiredVersion = REQUIRED_VERSION;
        requiredAddons[] = {"ams_main", "A3_3DEN"};
        author = "GrueArbre";
        VERSION_CONFIG;
    };
};

class ctrlMenuStrip;
class ctrlMenu;
class display3DEN
{
	class Controls
	{
		class MenuStrip: ctrlMenuStrip
		{
			class Items
			{
				class Tools
				{
					items[] += {"AMS_Export"};
				};
				class AMS_Export
				{
					text = "Export to GameRealisticMap Studio";
					action = QUOTE([] spawn FUNC(export););
				};
			};
		};
	};
	
	class ContextMenu : ctrlMenu
    {
        class Items
        {
            items[]+={"AMS_Transform"};
            class AMS_Transform
            {
                action=QUOTE([] spawn FUNC(transform););
                text="Re-create hidden objects";
                conditionShow="selectedLogic";
                value=0;
                opensNewWindow=0;
            };
        };
    };
};


class Cfg3DEN
{
	class Object
	{
		class AttributeCategories
		{
			class StateSpecial
			{
				class Attributes
				{
					class AMS_Exclude
					{
						displayName="Exclude from GameRealisticMap Studio Export";
						property="AMS_Exclude";
						control="Checkbox";
						expression="";
						defaultValue="false";
					};
				};
			};
		};
	};
};


class CfgVehicles {
	
	class Wall_F;
	class Land_New_WiredFence_5m_F: Wall_F 
	{
		class AmsEden
		{
			canexport = 1;
		};
    };
    class Land_New_WiredFence_10m_F: Wall_F 
	{
		class AmsEden
		{
			canexport = 1;
		};
    };
	
	class TargetBootcampHumanSimple_F;
	class TargetBootcampHuman_F: TargetBootcampHumanSimple_F 
	{
		class AmsEden
		{
			canexport = 1;
		};
	};
	
	class All;
	class AllVehicles: All 
	{
		class AmsEden
		{
			canexport = -1;
		};
    };

	class Shelter_base_F;
	class CamoNet_BLUFOR_F: Shelter_base_F 
	{
		class AmsEden
		{
			canexport = 1;
		};
    };
};

#include "CfgEventHandlers.hpp"


