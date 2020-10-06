class [MATERIALNAME] extends TemplateElement {
    constructor() {
        super();
        this.location = "interior";
        this.curTime = 0.0;
        this.bNeedUpdate = false;
        this._isConnected = false;
    }
    get templateID() { return "[MATERIALNAME]"; }
    connectedCallback() {
        super.connectedCallback();
        let parsedUrl = new URL(this.getAttribute("Url").toLowerCase());
        let updateLoop = () => {
            if (!this._isConnected)
                return;
            this.Update();
            requestAnimationFrame(updateLoop);
        };
        this._isConnected = true;
        requestAnimationFrame(updateLoop);
    }
    disconnectedCallback() {
    }
    Update() {
		this.updateInstruments();
    }
    /*playInstrumentSound(soundId) {
        if (this.isElectricityAvailable()) {
            Coherent.call("PLAY_INSTRUMENT_SOUND", soundId);
            return true;
        }
        return false;
    }	*/
    updateInstruments() {
[INSTRUMENTS]
    }
}
registerLivery("[MATERIALNAME]-element", [MATERIALNAME]);
