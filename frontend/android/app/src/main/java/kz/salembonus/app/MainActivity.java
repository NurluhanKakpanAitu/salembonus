package kz.salembonus.app;

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {

    @Override
    public void onCreate(Bundle savedInstanceState) {
        // Қосымшаның өз плагині (npm пакеті емес) — bridge көтерілмей тұрып тіркеледі.
        registerPlugin(PushSupportPlugin.class);
        super.onCreate(savedInstanceState);
    }
}
