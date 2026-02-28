pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
  }

  environment {
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_NOLOGO = '1'
    ARTIFACT_DIR = 'artifacts'
  }

  stages {
    stage('Prep') {
      steps {
        sh 'mkdir -p "${ARTIFACT_DIR}" "${ARTIFACT_DIR}/logs"'
      }
    }

    stage('Restore') {
      steps {
        sh './scripts/ci_stage.sh restore dotnet restore Tracker.slnx'
      }
    }

    stage('Build') {
      steps {
        sh './scripts/ci_stage.sh build dotnet build Tracker.slnx -v minimal'
      }
    }

    stage('Tests') {
      steps {
        sh './scripts/ci_stage.sh tests dotnet test Tracker.slnx -v minimal'
      }
    }

    stage('Deterministic Eval') {
      steps {
        sh './scripts/ci_stage.sh deterministic_eval ./scripts/run_deterministic_eval.sh'
      }
    }

    stage('Proof Of Life') {
      steps {
        sh 'SKIP_ANALYSIS=1 ./scripts/ci_stage.sh proof_of_life ./scripts/proof_of_life.sh'
      }
    }
  }

  post {
    always {
      script {
        def result = currentBuild.currentResult ?: 'UNKNOWN'
        sh """
          if command -v jq >/dev/null 2>&1; then
            jq -s \\
              --arg buildNumber "${env.BUILD_NUMBER}" \\
              --arg jobName "${env.JOB_NAME}" \\
              --arg buildUrl "${env.BUILD_URL}" \\
              --arg result "${result}" \\
              '{
                buildNumber: (\$buildNumber | tonumber),
                jobName: \$jobName,
                buildUrl: \$buildUrl,
                result: \$result,
                stages: .,
                stageCount: length,
                totalDurationMs: (map(.durationMs) | add // 0)
              }' "${ARTIFACT_DIR}/stage-metrics.jsonl" > "${ARTIFACT_DIR}/build-metrics.json"
          fi
        """
      }

      archiveArtifacts artifacts: 'artifacts/**', allowEmptyArchive: true, fingerprint: true
    }
  }
}
