package kz.salembonus.app;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

/**
 * Firebase билдке қосылған ба: google-services плагині google-services.json-нан google_app_id ресурсын жасайды.
 * Firebase-сіз push плагинінің register() әдісі қосымшаны құлатады, сондықтан қосымша (push.ts) алдымен осыны сұрайды.
 */
@CapacitorPlugin(name = "PushSupport")
public class PushSupportPlugin extends Plugin {

    @PluginMethod
    public void check(PluginCall call) {
        int id = getContext().getResources().getIdentifier("google_app_id", "string", getContext().getPackageName());
        JSObject result = new JSObject();
        result.put("available", id != 0);
        call.resolve(result);
    }
}
