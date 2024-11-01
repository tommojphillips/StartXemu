namespace StartXemu
{
    public struct XSetting
    {
        public string variableName;
        public string sectionName;
        public bool quoted;
        public string defaultValue;

        public XSetting(string section, string var, bool valueQuoted = true, string defaultValue = "")
        {
            sectionName = section;
            variableName = var;
            quoted = valueQuoted;
            this.defaultValue = defaultValue;

        }
    }
}
